namespace SocialMedia.Application.DTOs.TikTok;

public sealed class TikTokPublishOptions
{
    public string Caption { get; set; } = string.Empty;
    public string PrivacyLevel { get; set; } = "PUBLIC_TO_EVERYONE";
    public bool DisableComment { get; set; }
    public bool DisableDuet { get; set; }
    public bool DisableStitch { get; set; }
}

public sealed class TikTokPublishResult
{
    public string PublishId { get; set; } = string.Empty;
    public string? VideoId { get; set; }
}
