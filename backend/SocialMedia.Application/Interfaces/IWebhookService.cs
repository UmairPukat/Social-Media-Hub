using SocialMedia.Application.DTOs.Common;

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
    Task<string?> VerifyConnectionAsync(
        string? platformCode,
        string mode,
        string challenge,
        string verifyToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates Meta's X-Hub-Signature-256. Tries MetaSettings secrets and every App Connection secret.
    /// </summary>
    Task<bool> IsSignatureValidAsync(
        string? platformCode,
        string payloadJson,
        string? signature,
        CancellationToken cancellationToken = default);

    /// <summary>Reads Meta's <c>object</c> field so a shared webhook URL can route the delivery.</summary>
    string? DetectPlatformFromPayload(string payloadJson);

    /// <summary>Registers webhook subscription intent (stores verify config status).</summary>
    Task<ApiResponse<object>> SubscribeAsync(string platformCode, string? callbackUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Always saves the raw payload and a WebhookEvent, then processes. Pass
    /// <paramref name="signatureValid"/> as false to record a rejected delivery without processing it.
    /// </summary>
    Task<ApiResponse<object>> ReceiveAsync(
        string platformCode,
        string payloadJson,
        string? signature,
        string? headersJson,
        bool signatureValid = true,
        CancellationToken cancellationToken = default);

    /// <summary>Re-runs processing for a stored webhook event (e.g. after fixing signature or page connect).</summary>
    Task<ApiResponse<object>> ReprocessEventAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>Recent webhook events with status and processing notes for debugging.</summary>
    Task<ApiResponse<IReadOnlyList<object>>> GetRecentEventsAsync(int take = 25, CancellationToken cancellationToken = default);
}
