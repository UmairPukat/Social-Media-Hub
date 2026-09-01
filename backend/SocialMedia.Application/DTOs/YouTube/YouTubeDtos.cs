namespace SocialMedia.Application.DTOs.YouTube;

public class YouTubeSyncResultDto
{
    public string PlatformCode { get; set; } = "youtube";
    public string MenuType { get; set; } = string.Empty;
    public int Fetched { get; set; }
    public int Stored { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public string? Message { get; set; }
}

public class YouTubePostStatisticsDto
{
    public Guid PostId { get; set; }
    public string? ExternalPostId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Permalink { get; set; }
    public long ViewCount { get; set; }
    public long LikeCount { get; set; }
    public long CommentCount { get; set; }
    public long ShareCount { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? RefreshedAt { get; set; }
}
