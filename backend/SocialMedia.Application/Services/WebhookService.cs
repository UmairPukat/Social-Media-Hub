using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Settings;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Interfaces;

namespace SocialMedia.Application.Services;

/// <summary>
/// Webhook connection (verify), subscribe, and receive.
/// Flow: save full payload to WebhookLogs → save WebhookEvent → process → SignalR.
/// </summary>
public class WebhookService : IWebhookService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFacebookService _facebookService;
    private readonly IInstagramService _instagramService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly MetaSettings _meta;

    public WebhookService(
        IUnitOfWork unitOfWork,
        IFacebookService facebookService,
        IInstagramService instagramService,
        IWhatsAppService whatsAppService,
        IOptions<MetaSettings> metaOptions)
    {
        _unitOfWork = unitOfWork;
        _facebookService = facebookService;
        _instagramService = instagramService;
        _whatsAppService = whatsAppService;
        _meta = metaOptions.Value;
    }

    public string? VerifyConnection(string platformCode, string mode, string challenge, string verifyToken)
    {
        var expected = platformCode.ToLowerInvariant() switch
        {
            "facebook" => _meta.Facebook.WebhookVerifyToken,
            "instagram" => _meta.Instagram.WebhookVerifyToken,
            "whatsapp" => _meta.WhatsApp.WebhookVerifyToken,
            _ => null
        };

        if (mode == "subscribe" && expected is not null && verifyToken == expected)
            return challenge;

        return null;
    }

    public bool IsSignatureValid(string platformCode, string payloadJson, string? signature)
    {
        var appSecret = platformCode.ToLowerInvariant() switch
        {
            "facebook" => _meta.Facebook.AppSecret,
            "instagram" => !string.IsNullOrWhiteSpace(_meta.Instagram.AppSecret)
                ? _meta.Instagram.AppSecret
                : _meta.Facebook.AppSecret,
            "whatsapp" => _meta.WhatsApp.AppSecret,
            _ => string.Empty
        };
        if (string.IsNullOrWhiteSpace(appSecret) ||
            string.IsNullOrWhiteSpace(signature) ||
            !signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            return false;

        byte[] supplied;
        try
        {
            supplied = Convert.FromHexString(signature["sha256=".Length..]);
        }
        catch (FormatException)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadJson));
        return supplied.Length == expected.Length &&
               CryptographicOperations.FixedTimeEquals(supplied, expected);
    }

    public async Task<ApiResponse<object>> SubscribeAsync(string platformCode, string? callbackUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var platform = await _unitOfWork.Platforms.GetByCodeAsync(platformCode, cancellationToken);
            if (platform is null)
                return ApiResponse<object>.Fail("Unknown platform.");

            await _unitOfWork.WebhookEvents.AddAsync(new WebhookEvent
            {
                PlatformId = platform.Id,
                EventType = "subscribe",
                ObjectType = "webhook",
                PayloadJson = $"{{\"callbackUrl\":\"{callbackUrl}\",\"platform\":\"{platformCode}\"}}",
                Status = WebhookEventStatus.Processed,
                ReceivedAt = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ApiResponse<object>.Ok(new
            {
                platform = platformCode,
                verifyToken = platformCode.ToLowerInvariant() switch
                {
                    "facebook" => _meta.Facebook.WebhookVerifyToken,
                    "instagram" => _meta.Instagram.WebhookVerifyToken,
                    "whatsapp" => _meta.WhatsApp.WebhookVerifyToken,
                    _ => string.Empty
                },
                message = "Use this verify token in Meta Developer Console webhook settings."
            }, "Webhook subscribe recorded.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<object>> ReceiveAsync(
        string platformCode,
        string payloadJson,
        string? signature,
        string? headersJson,
        bool signatureValid = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var platform = await _unitOfWork.Platforms.GetByCodeAsync(platformCode, cancellationToken);

            // 1) Always persist the full raw payload to WebhookLogs first.
            var log = new WebhookLog
            {
                PlatformId = platform?.Id,
                PlatformCode = platformCode,
                Signature = signature,
                HeadersJson = headersJson,
                PayloadJson = payloadJson,
                ReceivedAt = DateTime.UtcNow
            };
            await _unitOfWork.WebhookLogs.AddAsync(log, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 2) Track processing status on WebhookEvents. Meta names the source in "object", which is
            // trusted over the endpoint that was hit so a mis-mapped callback URL still routes correctly.
            var descriptor = Describe(payloadJson);
            var targetCode = descriptor.PlatformCode ?? platformCode;
            var targetPlatform = string.Equals(targetCode, platformCode, StringComparison.OrdinalIgnoreCase)
                ? platform
                : await _unitOfWork.Platforms.GetByCodeAsync(targetCode, cancellationToken);

            var webhookEvent = new WebhookEvent
            {
                PlatformId = targetPlatform?.Id ?? platform?.Id,
                EventType = descriptor.EventType,
                ObjectType = targetCode,
                ExternalObjectId = descriptor.EntryId,
                PayloadJson = payloadJson,
                Signature = signature,
                HeadersJson = headersJson,
                Status = WebhookEventStatus.Received,
                ReceivedAt = DateTime.UtcNow
            };
            await _unitOfWork.WebhookEvents.AddAsync(webhookEvent, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 3) A rejected signature is recorded rather than dropped, so a delivery is never invisible.
            if (!signatureValid)
            {
                webhookEvent.Status = WebhookEventStatus.Failed;
                webhookEvent.Error = "Rejected: X-Hub-Signature-256 missing or does not match the configured app secret.";
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                _unitOfWork.WebhookEvents.Update(webhookEvent);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<object>.Fail("Invalid webhook signature.");
            }

            webhookEvent.Status = WebhookEventStatus.Processing;
            _unitOfWork.WebhookEvents.Update(webhookEvent);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            try
            {
                var result = targetCode.ToLowerInvariant() switch
                {
                    "facebook" => await _facebookService.ProcessWebhookPayloadAsync(webhookEvent, cancellationToken),
                    "instagram" => await _instagramService.ProcessWebhookPayloadAsync(webhookEvent, cancellationToken),
                    "whatsapp" => await _whatsAppService.ProcessWebhookPayloadAsync(webhookEvent, cancellationToken),
                    _ => null
                };

                webhookEvent.Status = WebhookEventStatus.Processed;
                webhookEvent.ProcessedAt = DateTime.UtcNow;

                // Nothing stored is the common silent failure, so the reason is written to the event.
                if (result is null)
                    webhookEvent.Error = $"No processor is registered for platform '{targetCode}'.";
                else if (result.Handled == 0)
                    webhookEvent.Error = result.Notes.Count > 0
                        ? "Nothing stored. " + string.Join(" | ", result.Notes)
                        : "Nothing stored. Payload contained no recognised items.";
            }
            catch (Exception ex)
            {
                webhookEvent.Status = WebhookEventStatus.Failed;
                webhookEvent.Error = ex.Message;
                webhookEvent.RetryCount += 1;
            }

            _unitOfWork.WebhookEvents.Update(webhookEvent);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ApiResponse<object>.Ok(new
            {
                logId = log.Id,
                webhookEvent.Id,
                webhookEvent.Status,
                note = webhookEvent.Error
            }, "Webhook received.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Pulls the source platform, entry id and subscribed field names out of a delivery so a
    /// WebhookEvent row can be identified at a glance instead of only by its raw payload.
    /// </summary>
    private static (string EventType, string? EntryId, string? PlatformCode) Describe(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);

            var platformCode = doc.RootElement.TryGetProperty("object", out var objectElement)
                ? objectElement.GetString() switch
                {
                    "page" => "facebook",
                    "instagram" => "instagram",
                    "whatsapp_business_account" => "whatsapp",
                    _ => null
                }
                : null;

            if (!doc.RootElement.TryGetProperty("entry", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
                return ("received", null, platformCode);

            string? entryId = null;
            var fields = new List<string>();

            foreach (var entry in entries.EnumerateArray())
            {
                entryId ??= entry.TryGetProperty("id", out var id) ? id.ToString() : null;

                if (entry.TryGetProperty("changes", out var changes) && changes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var change in changes.EnumerateArray())
                    {
                        var field = change.TryGetProperty("field", out var f) ? f.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(field) && !fields.Contains(field!))
                            fields.Add(field!);
                    }
                }

                if (entry.TryGetProperty("messaging", out var messaging) &&
                    messaging.ValueKind == JsonValueKind.Array &&
                    !fields.Contains("messaging"))
                    fields.Add("messaging");
            }

            var eventType = fields.Count > 0 ? string.Join(",", fields) : "received";
            return (eventType.Length > 100 ? eventType[..100] : eventType, entryId, platformCode);
        }
        catch (JsonException)
        {
            return ("received", null, null);
        }
    }
}
