using SocialMedia.Domain.Common;

namespace SocialMedia.Domain.Entities;

/// <summary>
/// Append-only raw webhook payload log. Always written before business processing.
/// </summary>
public class WebhookLog : BaseEntity
{
    public Guid? PlatformId { get; set; }
    public string PlatformCode { get; set; } = string.Empty;
    public string? Signature { get; set; }
    public string? HeadersJson { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public Platform? Platform { get; set; }
}
