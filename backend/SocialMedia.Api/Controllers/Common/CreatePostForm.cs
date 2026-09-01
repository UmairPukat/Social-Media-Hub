namespace SocialMedia.Api.Controllers.Common;

public sealed class CreatePostForm
{
    public Guid SocialProfileId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string? Title { get; set; }
    public string? Visibility { get; set; }
    public IFormFile? MediaFile { get; set; }
}
