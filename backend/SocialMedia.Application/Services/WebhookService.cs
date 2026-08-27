using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;
using SocialMedia.Application.Settings;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Interfaces;

namespace SocialMedia.Application.Services;

/// <summary>
/// Webhook connection (verify), subscribe, and receive.
/// Flow: classify inbound user content → save WebhookEvent → process → update WebhookEvent.
/// </summary>
public class WebhookService : IWebhookService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFacebookService _facebookService;
    private readonly IInstagramService _instagramService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly MetaSettings _meta;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(
        IUnitOfWork unitOfWork,
        IFacebookService facebookService,
        IInstagramService instagramService,
        IWhatsAppService whatsAppService,
        IOptions<MetaSettings> metaOptions,
        ILogger<WebhookService> logger)
    {
        _unitOfWork = unitOfWork;
        _facebookService = facebookService;
        _instagramService = instagramService;
        _whatsAppService = whatsAppService;
        _meta = metaOptions.Value;
        _logger = logger;
    }

    public string? VerifyConnection(string? platformCode, string mode, string challenge, string verifyToken)
    {
        if (mode != "subscribe" || string.IsNullOrWhiteSpace(verifyToken) || string.IsNullOrWhiteSpace(challenge))
            return null;

        var code = platformCode?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(code) || code is "meta" or "all")
        {
            // Shared callback: accept whichever product Meta is verifying.
            var tokens = new[]
            {
                _meta.Facebook.WebhookVerifyToken,
                _meta.Instagram.WebhookVerifyToken,
                _meta.InstagramLogin.WebhookVerifyToken,
                _meta.WhatsApp.WebhookVerifyToken
            };
            return tokens.Any(t => !string.IsNullOrWhiteSpace(t) && t == verifyToken)
                ? challenge
                : null;
        }

        var expected = code switch
        {
            "facebook" => _meta.Facebook.WebhookVerifyToken,
            "instagram" => _meta.Instagram.WebhookVerifyToken,
            "instagram_login" => _meta.InstagramLogin.WebhookVerifyToken,
            "whatsapp" => _meta.WhatsApp.WebhookVerifyToken,
            _ => null
        };

        return expected is not null && verifyToken == expected ? challenge : null;
    }

    public bool IsSignatureValid(string? platformCode, string payloadJson, string? signature)
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

        foreach (var appSecret in ResolveAppSecrets(platformCode, payloadJson))
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

    private IEnumerable<string> ResolveAppSecrets(string? platformCode, string payloadJson)
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
                // Both Instagram connection types deliver object="instagram" on the shared callback,
                // but each is signed by its own app, so every Instagram-capable secret is a candidate.
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

    public Task<ApiResponse<object>> ReceiveForProcessAsync(
        string menuType,
        string platformCode,
        string payloadJson,
        string? signature,
        string? headersJson,
        bool signatureValid = true,
        CancellationToken cancellationToken = default)
        => ReceiveAsync(
            platformCode,
            payloadJson,
            signature,
            headersJson,
            signatureValid,
            MenuTypes.Normalize(menuType),
            cancellationToken);

    public async Task<ApiResponse<object>> ReceiveAsync(
        string platformCode,
        string payloadJson,
        string? signature,
        string? headersJson,
        bool signatureValid = true,
        string? menuType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMenu = string.IsNullOrWhiteSpace(menuType) ? null : MenuTypes.Normalize(menuType);

            if (!signatureValid)
            {
                _logger.LogWarning(
                    "Webhook rejected — invalid X-Hub-Signature-256 for module {MenuType}, platform {PlatformCode}.",
                    normalizedMenu,
                    platformCode);
                return ApiResponse<object>.Fail("Invalid webhook signature.");
            }

            var platform = await _unitOfWork.Platforms.GetByCodeAsync(platformCode, normalizedMenu, cancellationToken: cancellationToken);

            // Meta names the source in "object", which is trusted over the endpoint that was hit.
            var descriptor = Describe(payloadJson);
            var targetCode = descriptor.PlatformCode ?? platformCode;
            var targetPlatform = string.Equals(targetCode, platformCode, StringComparison.OrdinalIgnoreCase)
                ? platform
                : await _unitOfWork.Platforms.GetByCodeAsync(targetCode, normalizedMenu, cancellationToken: cancellationToken);

            var webhookEvent = new WebhookEvent
            {
                PlatformId = targetPlatform?.Id ?? platform?.Id,
                EventType = descriptor.EventType,
                ObjectType = targetCode,
                ExternalObjectId = descriptor.EntryId,
                PayloadJson = payloadJson,
                Signature = signature,
                HeadersJson = headersJson,
                MenuType = normalizedMenu,
                Status = WebhookEventStatus.Received,
                ReceivedAt = DateTime.UtcNow
            };

            if (!MetaWebhookContentClassifier.ContainsRealUserInboundContent(payloadJson))
            {
                _logger.LogInformation(
                    "Webhook ignored for module {MenuType} — not a real user message/comment. Object={ObjectType}, EntryId={EntryId}",
                    normalizedMenu,
                    descriptor.PlatformCode ?? platformCode,
                    descriptor.EntryId);
                return ApiResponse<object>.Ok(new
                {
                    stored = false,
                    handled = 0,
                    note = "Ignored — not an inbound user message or comment."
                }, "Webhook ignored — not from a real user.");
            }

            await _unitOfWork.WebhookEvents.AddAsync(webhookEvent, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Real-user webhook saved for module {MenuType}. WebhookEventId={WebhookEventId}, Object={ObjectType}, EntryId={EntryId}",
                normalizedMenu,
                webhookEvent.Id,
                targetCode,
                descriptor.EntryId);

            webhookEvent.Status = WebhookEventStatus.Processing;
            _unitOfWork.WebhookEvents.Update(webhookEvent);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            WebhookProcessResult? result;
            try
            {
                result = await ProcessMetaPayloadAsync(webhookEvent, targetCode, cancellationToken);

                webhookEvent.Status = WebhookEventStatus.Processed;
                webhookEvent.ProcessedAt = DateTime.UtcNow;

                if (result is null)
                    webhookEvent.Error = $"No processor is registered for platform '{targetCode}'.";
            }
            catch (Exception ex)
            {
                webhookEvent.Status = WebhookEventStatus.Failed;
                webhookEvent.Error = ex.Message;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                webhookEvent.RetryCount += 1;
                result = null;
            }

            if (result?.Handled == 0)
            {
                webhookEvent.Error = result?.Notes.Count > 0
                    ? "Nothing stored. " + string.Join(" | ", result.Notes)
                    : webhookEvent.Error ?? "Nothing stored.";
            }

            var log = new WebhookLog
            {
                PlatformId = webhookEvent.PlatformId,
                PlatformCode = platformCode,
                Signature = signature,
                HeadersJson = headersJson,
                PayloadJson = payloadJson,
                ReceivedAt = webhookEvent.ReceivedAt
            };
            await _unitOfWork.WebhookLogs.AddAsync(log, cancellationToken);
            _unitOfWork.WebhookEvents.Update(webhookEvent);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Webhook processed for module {MenuType}. WebhookEventId={WebhookEventId}, Handled={Handled}, Error={Error}",
                normalizedMenu,
                webhookEvent.Id,
                result?.Handled ?? 0,
                webhookEvent.Error);

            return ApiResponse<object>.Ok(new
            {
                stored = true,
                logId = log.Id,
                webhookEventId = webhookEvent.Id,
                webhookEvent.Status,
                handled = result?.Handled ?? 0
            }, "Webhook received.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    private async Task<WebhookProcessResult?> ProcessMetaPayloadAsync(
        WebhookEvent webhookEvent,
        string targetCode,
        CancellationToken cancellationToken)
    {
        var processors = targetCode.ToLowerInvariant() switch
        {
            "facebook" => new[] { "facebook", "instagram" },
            "instagram" => new[] { "instagram", "facebook" },
            "whatsapp" => new[] { "whatsapp" },
            _ => new[] { "instagram", "facebook", "whatsapp" }
        };

        WebhookProcessResult? last = null;
        foreach (var code in processors)
        {
            var attempt = code switch
            {
                "facebook" => await _facebookService.ProcessWebhookPayloadAsync(webhookEvent, cancellationToken),
                "instagram" => await _instagramService.ProcessWebhookPayloadAsync(webhookEvent, cancellationToken),
                "whatsapp" => await _whatsAppService.ProcessWebhookPayloadAsync(webhookEvent, cancellationToken),
                _ => null
            };

            if (attempt?.Handled > 0)
                return attempt;

            if (attempt is not null)
            {
                if (last is null)
                    last = attempt;
                else
                    foreach (var note in attempt.Notes)
                        last.Skip(note);
            }
        }

        return last;
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

    public async Task<string?> VerifyConnectionForProcessAsync(
        string menuType,
        string mode,
        string challenge,
        string verifyToken,
        CancellationToken cancellationToken = default)
    {
        if (mode != "subscribe" || string.IsNullOrWhiteSpace(verifyToken) || string.IsNullOrWhiteSpace(challenge))
            return null;

        var tokens = await LoadVerifyTokensForProcessAsync(menuType, cancellationToken);
        return tokens.Any(t => !string.IsNullOrWhiteSpace(t) && t == verifyToken) ? challenge : null;
    }

    public async Task<bool> IsSignatureValidForProcessAsync(
        string menuType,
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

        var secrets = await LoadSecretsForProcessAsync(menuType, platformCode, payloadJson, cancellationToken);
        foreach (var appSecret in secrets)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
            var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadJson));
            if (supplied.Length == expected.Length &&
                CryptographicOperations.FixedTimeEquals(supplied, expected))
                return true;
        }

        return false;
    }

    private async Task<IReadOnlyList<string>> LoadVerifyTokensForProcessAsync(
        string menuType,
        CancellationToken cancellationToken)
    {
        var normalized = MenuTypes.Normalize(menuType);
        var tokens = new List<string>();
        void Add(string? token)
        {
            if (!string.IsNullOrWhiteSpace(token) && !tokens.Contains(token))
                tokens.Add(token!);
        }

        if (normalized == MenuTypes.Integration)
        {
            Add(_meta.Facebook.WebhookVerifyToken);
            Add(_meta.Instagram.WebhookVerifyToken);
            Add(_meta.InstagramLogin.WebhookVerifyToken);
            Add(_meta.WhatsApp.WebhookVerifyToken);
            foreach (var token in await _unitOfWork.IntegrationAppConfigs.GetWebhookVerifyTokensAsync(normalized, cancellationToken))
                Add(token);
        }
        else if (normalized == MenuTypes.AppConnection)
        {
            foreach (var token in await _unitOfWork.AppConnectionConfigs.GetWebhookVerifyTokensAsync(normalized, cancellationToken))
                Add(token);
        }
        else
        {
            foreach (var token in await _unitOfWork.DeveloperAppConfigs.GetWebhookVerifyTokensAsync(normalized, cancellationToken))
                Add(token);
        }

        return tokens;
    }

    private async Task<IReadOnlyList<string>> LoadSecretsForProcessAsync(
        string menuType,
        string? platformCode,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var normalized = MenuTypes.Normalize(menuType);
        if (normalized == MenuTypes.Integration)
            return ResolveAppSecrets(platformCode, payloadJson).ToList();

        var secrets = new List<string>();
        void Add(string? secret)
        {
            if (!string.IsNullOrWhiteSpace(secret) && !secrets.Contains(secret))
                secrets.Add(secret);
        }

        var dbSecrets = normalized == MenuTypes.AppConnection
            ? await _unitOfWork.AppConnectionConfigs.GetClientSecretsAsync(normalized, cancellationToken)
            : await _unitOfWork.DeveloperAppConfigs.GetClientSecretsAsync(normalized, cancellationToken);

        foreach (var secret in dbSecrets)
            Add(secret);

        foreach (var secret in ResolveAppSecrets(platformCode, payloadJson))
            Add(secret);

        return secrets;
    }
}
