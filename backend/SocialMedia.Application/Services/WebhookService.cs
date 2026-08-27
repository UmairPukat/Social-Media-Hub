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

    public async Task<string?> VerifyConnectionAsync(
        string? platformCode,
        string mode,
        string challenge,
        string verifyToken,
        CancellationToken cancellationToken = default)
    {
        if (mode != "subscribe" || string.IsNullOrWhiteSpace(verifyToken) || string.IsNullOrWhiteSpace(challenge))
            return null;

        var tokens = await LoadVerifyTokensAsync(platformCode, cancellationToken);
        return tokens.Any(t => t == verifyToken) ? challenge : null;
    }

    public async Task<bool> IsSignatureValidAsync(
        string? platformCode,
        string payloadJson,
        string? signature,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(signature) ||
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

        foreach (var appSecret in await ResolveAppSecretsAsync(platformCode, payloadJson, cancellationToken))
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
            var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadJson));
            if (supplied.Length == expected.Length &&
                CryptographicOperations.FixedTimeEquals(supplied, expected))
                return true;
        }

        return false;
    }

    public string? DetectPlatformFromPayload(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty("object", out var objectElement))
                return null;

            return objectElement.GetString() switch
            {
                "page" => "facebook",
                "instagram" => "instagram",
                "whatsapp_business_account" => "whatsapp",
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<string>> LoadVerifyTokensAsync(
        string? platformCode,
        CancellationToken cancellationToken)
    {
        var tokens = new List<string>();
        void Add(string? token)
        {
            if (!string.IsNullOrWhiteSpace(token) && !tokens.Contains(token))
                tokens.Add(token);
        }

        var code = platformCode?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(code) || code is "meta" or "all")
        {
            Add(_meta.Facebook.WebhookVerifyToken);
            Add(_meta.Instagram.WebhookVerifyToken);
            Add(_meta.InstagramLogin.WebhookVerifyToken);
            Add(_meta.WhatsApp.WebhookVerifyToken);
        }
        else
        {
            var expected = code switch
            {
                "facebook" => _meta.Facebook.WebhookVerifyToken,
                "instagram" => _meta.Instagram.WebhookVerifyToken,
                "instagram_login" => _meta.InstagramLogin.WebhookVerifyToken,
                "whatsapp" => _meta.WhatsApp.WebhookVerifyToken,
                _ => null
            };
            Add(expected);
        }

        var appConfigs = await _unitOfWork.AppConnectionConfigs.FindAsync(
            c => c.WebhookVerifyToken != null && c.WebhookVerifyToken != string.Empty,
            cancellationToken);
        foreach (var config in appConfigs)
            Add(config.WebhookVerifyToken);

        return tokens;
    }

    private async Task<IReadOnlyList<string>> ResolveAppSecretsAsync(
        string? platformCode,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var code = platformCode?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(code) || code is "meta" or "all")
            code = DetectPlatformFromPayload(payloadJson);

        var secrets = new List<string>();
        void Add(string? secret)
        {
            if (!string.IsNullOrWhiteSpace(secret) && !secrets.Contains(secret))
                secrets.Add(secret);
        }

        switch (code)
        {
            case "facebook":
                Add(_meta.Facebook.AppSecret);
                break;
            case "instagram":
            case "instagram_login":
                Add(!string.IsNullOrWhiteSpace(_meta.Instagram.AppSecret)
                    ? _meta.Instagram.AppSecret
                    : _meta.Facebook.AppSecret);
                Add(_meta.InstagramLogin.AppSecret);
                Add(_meta.Facebook.AppSecret);
                break;
            case "whatsapp":
                Add(_meta.WhatsApp.AppSecret);
                Add(_meta.Facebook.AppSecret);
                break;
            default:
                Add(_meta.Facebook.AppSecret);
                Add(_meta.Instagram.AppSecret);
                Add(_meta.InstagramLogin.AppSecret);
                Add(_meta.WhatsApp.AppSecret);
                break;
        }

        // App Connections may use a different Meta app than MetaSettings — include every stored secret.
        var appConfigs = await _unitOfWork.AppConnectionConfigs.FindAsync(
            c => c.ClientSecret != null && c.ClientSecret != string.Empty,
            cancellationToken);
        foreach (var config in appConfigs)
            Add(config.ClientSecret);

        return secrets;
    }

    public async Task<ApiResponse<object>> SubscribeAsync(string platformCode, string? callbackUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var platform = await _unitOfWork.Platforms.GetByCodeAsync(platformCode, cancellationToken: cancellationToken);
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
                    "instagram_login" => _meta.InstagramLogin.WebhookVerifyToken,
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
            var platform = await _unitOfWork.Platforms.GetByCodeAsync(platformCode, cancellationToken: cancellationToken);

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

            var descriptor = Describe(payloadJson);
            var targetCode = descriptor.PlatformCode ?? platformCode;
            var targetPlatform = string.Equals(targetCode, platformCode, StringComparison.OrdinalIgnoreCase)
                ? platform
                : await _unitOfWork.Platforms.GetByCodeAsync(targetCode, cancellationToken: cancellationToken);

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

            if (!signatureValid)
            {
                webhookEvent.Status = WebhookEventStatus.Failed;
                webhookEvent.Error =
                    "Rejected: X-Hub-Signature-256 missing or does not match any configured app secret (Integrations or App Connections).";
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                _unitOfWork.WebhookEvents.Update(webhookEvent);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<object>.Fail("Invalid webhook signature.");
            }

            await ProcessWebhookEventAsync(webhookEvent, targetCode, cancellationToken);

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

    public async Task<ApiResponse<object>> ReprocessEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        try
        {
            var webhookEvent = await _unitOfWork.WebhookEvents.GetByIdAsync(eventId, cancellationToken);
            if (webhookEvent is null)
                return ApiResponse<object>.Fail("Webhook event not found.");

            var targetCode = webhookEvent.ObjectType
                ?? DetectPlatformFromPayload(webhookEvent.PayloadJson)
                ?? "meta";

            if (!string.IsNullOrWhiteSpace(webhookEvent.Signature))
            {
                var signatureValid = await IsSignatureValidAsync(targetCode, webhookEvent.PayloadJson, webhookEvent.Signature, cancellationToken);
                if (!signatureValid)
                {
                    return ApiResponse<object>.Fail(
                        "Signature still invalid. Ensure the App Secret in Meta Developer Console matches Integrations settings or your App Connections config.");
                }
            }

            webhookEvent.Error = null;
            webhookEvent.RetryCount += 1;
            await ProcessWebhookEventAsync(webhookEvent, targetCode, cancellationToken);

            return ApiResponse<object>.Ok(new
            {
                webhookEvent.Id,
                webhookEvent.Status,
                note = webhookEvent.Error
            }, "Webhook reprocessed.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<IReadOnlyList<object>>> GetRecentEventsAsync(
        int take = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var events = await _unitOfWork.WebhookEvents.GetRecentAsync(Math.Clamp(take, 1, 100), cancellationToken);
            var rows = events.Select(e => (object)new
            {
                e.Id,
                e.EventType,
                e.ObjectType,
                e.ExternalObjectId,
                status = e.Status.ToString(),
                e.Error,
                e.ReceivedAt,
                e.ProcessedAt,
                e.RetryCount
            }).ToList();

            return ApiResponse<IReadOnlyList<object>>.Ok(rows);
        }
        catch (Exception ex)
        {
            return ApiResponse<IReadOnlyList<object>>.Fail(ex.Message);
        }
    }

    private async Task ProcessWebhookEventAsync(
        WebhookEvent webhookEvent,
        string targetCode,
        CancellationToken cancellationToken)
    {
        webhookEvent.Status = WebhookEventStatus.Processing;
        webhookEvent.ProcessedAt = null;
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

            if (result is null)
                webhookEvent.Error = $"No processor is registered for platform '{targetCode}'.";
            else if (result.Handled == 0)
                webhookEvent.Error = result.Notes.Count > 0
                    ? "Nothing stored. " + string.Join(" | ", result.Notes)
                    : "Nothing stored. Payload contained no recognised items.";
            else
                webhookEvent.Error = null;
        }
        catch (Exception ex)
        {
            webhookEvent.Status = WebhookEventStatus.Failed;
            webhookEvent.Error = ex.Message;
            webhookEvent.ProcessedAt = DateTime.UtcNow;
        }

        _unitOfWork.WebhookEvents.Update(webhookEvent);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

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
