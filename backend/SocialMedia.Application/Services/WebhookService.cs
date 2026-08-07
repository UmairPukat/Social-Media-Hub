using System.Security.Cryptography;
using System.Text;
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

            // 2) Track processing status on WebhookEvents.
            var webhookEvent = new WebhookEvent
            {
                PlatformId = platform?.Id,
                EventType = "received",
                ObjectType = platformCode,
                PayloadJson = payloadJson,
                Signature = signature,
                HeadersJson = headersJson,
                Status = WebhookEventStatus.Received,
                ReceivedAt = DateTime.UtcNow
            };
            await _unitOfWork.WebhookEvents.AddAsync(webhookEvent, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            webhookEvent.Status = WebhookEventStatus.Processing;
            _unitOfWork.WebhookEvents.Update(webhookEvent);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            try
            {
                switch (platformCode.ToLowerInvariant())
                {
                    case "facebook":
                        await _facebookService.ProcessWebhookPayloadAsync(webhookEvent, cancellationToken);
                        break;
                    case "instagram":
                        await _instagramService.ProcessWebhookPayloadAsync(webhookEvent, cancellationToken);
                        break;
                    case "whatsapp":
                        await _whatsAppService.ProcessWebhookPayloadAsync(webhookEvent, cancellationToken);
                        break;
                }

                webhookEvent.Status = WebhookEventStatus.Processed;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
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
                webhookEvent.Status
            }, "Webhook received.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }
}
