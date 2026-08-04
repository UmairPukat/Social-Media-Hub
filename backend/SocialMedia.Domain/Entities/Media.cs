using SocialMedia.Domain.Common;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Domain.Entities;

/// <summary>
/// Media attached to a Post.
/// </summary>
public class Media : BaseEntity
{
    public Guid PostId { get; set; }

    public string? ExternalMediaId { get; set; }
    public MediaType MediaType { get; set; } = MediaType.Image;
    public string Url { get; set; } = string.Empty;
    public string? Thumbnail { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Duration { get; set; }
    public int DisplayOrder { get; set; }

    public Post? Post { get; set; }
}
