namespace SocialMedia.Application.DTOs.Meta;

public class ReadInsightsDto
{
    public string Platform { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public int Days { get; set; }
    public IReadOnlyList<string> AvailablePlatforms { get; set; } = Array.Empty<string>();
    public bool PageViewsSupported { get; set; }
    public long? PageViews { get; set; }
    public bool ReachSupported { get; set; }
    public long? Reach { get; set; }
    public bool ImpressionsSupported { get; set; }
    public long? Impressions { get; set; }
    public bool EngagementSupported { get; set; }
    public long? Engagement { get; set; }
    public bool FollowersSupported { get; set; }
    public long? Followers { get; set; }
    public long? FollowerGrowth { get; set; }
    public IReadOnlyList<ReadInsightPointDto> Trend { get; set; } = Array.Empty<ReadInsightPointDto>();
    public IReadOnlyList<ReadInsightPostDto> Posts { get; set; } = Array.Empty<ReadInsightPostDto>();
    public IReadOnlyList<string> Notes { get; set; } = Array.Empty<string>();
}

public class ReadInsightPointDto
{
    public string Date { get; set; } = string.Empty;
    public long? PageViews { get; set; }
    public long? Reach { get; set; }
    public long? Impressions { get; set; }
    public long? Engagement { get; set; }
    public long? Followers { get; set; }
}

public class ReadInsightPostDto
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? CreatedTime { get; set; }
    public string? PictureUrl { get; set; }
    public long? Impressions { get; set; }
    public long? Reach { get; set; }
    public long Reactions { get; set; }
    public long Comments { get; set; }
    public long Shares { get; set; }
    public long Engagement { get; set; }
}
