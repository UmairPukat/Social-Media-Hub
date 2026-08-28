using SocialMedia.Domain.Common;

namespace SocialMedia.Domain.Modules.Common.Entities;

public abstract class WebhookLogEntityBase : BaseEntity
{
    public Guid? PlatformId { get; set; }
    public string PlatformCode { get; set; } = string.Empty;
    public string? Signature { get; set; }
    public string? HeadersJson { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
