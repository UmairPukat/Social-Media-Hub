using SocialMedia.Domain.Common;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Domain.Entities;

/// <summary>
/// Raw inbound webhook. Always save first, then process via background queue.
/// </summary>
public class WebhookEvent : BaseEntity
{
    public Guid? PlatformId { get; set; }

    public string EventType { get; set; } = string.Empty;
    public string? ObjectType { get; set; }
    public string? ExternalObjectId { get; set; }

    public string? HeadersJson { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public string? Signature { get; set; }

    public WebhookEventStatus Status { get; set; } = WebhookEventStatus.Received;
    public int RetryCount { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }

    /// <summary>Process module webhook endpoint that received this event.</summary>
    public string? MenuType { get; set; }

    public Platform? Platform { get; set; }
}
