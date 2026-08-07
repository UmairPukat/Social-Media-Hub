using SocialMedia.Application.DTOs.Common;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Application.Interfaces;

/// <summary>
/// Webhook subscribe / connection (verify) / received. Auth URLs live on the frontend.
/// </summary>
public interface IWebhookService
{
    /// <summary>Returns challenge when Meta verifies the webhook subscription.</summary>
    string? VerifyConnection(string platformCode, string mode, string challenge, string verifyToken);

    /// <summary>Validates Meta's X-Hub-Signature-256 against the raw request body.</summary>
    bool IsSignatureValid(string platformCode, string payloadJson, string? signature);

    /// <summary>Registers webhook subscription intent (stores verify config status).</summary>
    Task<ApiResponse<object>> SubscribeAsync(string platformCode, string? callbackUrl, CancellationToken cancellationToken = default);

    /// <summary>Always saves WebhookEvent first, then queues processing.</summary>
    Task<ApiResponse<object>> ReceiveAsync(string platformCode, string payloadJson, string? signature, string? headersJson, CancellationToken cancellationToken = default);
}
