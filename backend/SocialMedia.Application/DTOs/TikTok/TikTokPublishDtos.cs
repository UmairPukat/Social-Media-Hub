namespace SocialMedia.Application.DTOs.TikTok;

public sealed class TikTokCreatorInfo
{
    public IReadOnlyList<string> PrivacyLevelOptions { get; set; } = Array.Empty<string>();
    public bool CommentDisabled { get; set; }
    public bool DuetDisabled { get; set; }
    public bool StitchDisabled { get; set; }
}

public sealed class TikTokPublishOptions
{
    /// <summary>Video caption, or photo title (max 90 UTF-16 runes).</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Photo description / hashtags (max 4000 UTF-16 runes).</summary>
    public string Description { get; set; } = string.Empty;
    public string PrivacyLevel { get; set; } = "PUBLIC_TO_EVERYONE";
    public bool DisableComment { get; set; }
    public bool DisableDuet { get; set; }
    public bool DisableStitch { get; set; }
    public bool BrandContentToggle { get; set; }
    public bool BrandOrganicToggle { get; set; }
    public bool AutoAddMusic { get; set; } = true;
}

public sealed class TikTokPublishResult
{
    public string PublishId { get; set; } = string.Empty;
    public string? VideoId { get; set; }
}
