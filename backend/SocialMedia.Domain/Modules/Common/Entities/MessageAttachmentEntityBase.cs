using SocialMedia.Domain.Common;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Domain.Modules.Common.Entities;

public abstract class MessageAttachmentEntityBase : BaseEntity
{
    public Guid MessageId { get; set; }
    public MediaType Type { get; set; } = MediaType.Image;
    public string Url { get; set; } = string.Empty;
    public string? Thumbnail { get; set; }
    public long? Size { get; set; }
}
