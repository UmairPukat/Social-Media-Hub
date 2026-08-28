using SocialMedia.Domain.Common;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Domain.Modules.Common.Entities;

public abstract class PostEntityBase : BaseEntity
{
    public Guid SocialProfileId { get; set; }
    public Guid PlatformId { get; set; }
    public string? ExternalPostId { get; set; }
    public string? Text { get; set; }
    public string? Caption { get; set; }
    public ContentPostType Type { get; set; } = ContentPostType.Text;
    public ContentPostStatus Status { get; set; } = ContentPostStatus.Draft;
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public int ShareCount { get; set; }
    public int ViewCount { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? MetadataJson { get; set; }
    public string? ErrorMessage { get; set; }
}
