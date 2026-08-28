using SocialMedia.Domain.Common;

namespace SocialMedia.Domain.Modules.Common.Entities;

public abstract class CommentEntityBase : BaseEntity
{
    public Guid PostId { get; set; }
    public Guid? ParentCommentId { get; set; }
    public string ExternalCommentId { get; set; } = string.Empty;
    public string? AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorImage { get; set; }
    public string Message { get; set; } = string.Empty;
    public int LikeCount { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsHidden { get; set; }
    public DateTime? PlatformCreatedAt { get; set; }
}
