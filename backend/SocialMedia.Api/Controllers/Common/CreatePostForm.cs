namespace SocialMedia.Api.Controllers.Common;

public sealed class CreatePostForm
{
    public Guid SocialProfileId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string? Title { get; set; }
    public string? Visibility { get; set; }
    /// <summary>TikTok privacy: public, friends, followers, or only_you.</summary>
    public string? Privacy { get; set; }
    public bool? AllowComment { get; set; }
    public bool? AllowDuet { get; set; }
    public bool? AllowStitch { get; set; }
    public bool? DiscloseContent { get; set; }
    public bool? YourBrand { get; set; }
    public bool? BrandedContent { get; set; }
    public bool? AutoAddMusic { get; set; }
    public IFormFile? MediaFile { get; set; }
}
