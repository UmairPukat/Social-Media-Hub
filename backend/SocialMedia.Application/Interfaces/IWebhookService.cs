using SocialMedia.Application.DTOs.Common;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Application.Interfaces;

/// <summary>
/// Webhook subscribe / connection (verify) / received. Auth URLs live on the frontend.
/// </summary>
public interface IWebhookService
{
    /// <summary>
    /// Returns the challenge when Meta verifies a webhook subscription.
    /// Pass <paramref name="platformCode"/> as null/"meta" to accept any configured verify token
    /// (used by the shared <c>/api/webhooks</c> callback).
    /// </summary>
    string? VerifyConnection(string? platformCode, string mode, string challenge, string verifyToken);

    /// <summary>
    /// Validates Meta's X-Hub-Signature-256. When platform is unknown, tries every configured app secret.
    /// </summary>
    bool IsSignatureValid(string? platformCode, string payloadJson, string? signature);

    /// <summary>Reads Meta's <c>object</c> field so a shared webhook URL can route the delivery.</summary>
    string? DetectPlatformFromPayload(string payloadJson);

    /// <summary>Registers webhook subscription intent (stores verify config status).</summary>
    Task<ApiResponse<object>> SubscribeAsync(string platformCode, string? callbackUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Always saves the raw payload and a WebhookEvent, then processes. Pass
    /// <paramref name="signatureValid"/> as false to record a rejected delivery without processing it.
    /// </summary>
    Task<ApiResponse<object>> ReceiveAsync(string platformCode, string payloadJson, string? signature, string? headersJson, bool signatureValid = true, CancellationToken cancellationToken = default);
}
