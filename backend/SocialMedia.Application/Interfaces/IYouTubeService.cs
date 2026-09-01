using SocialMedia.Application.DTOs.Meta;

namespace SocialMedia.Application.Interfaces;

public interface IYouTubeService
{
    Task<OAuthTokenResult> ExchangeAuthorizationCodeAsync(
        string clientId,
        string clientSecret,
        string redirectUri,
        string code,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SocialProfileDraft>> DiscoverChannelsAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<YouTubeVideoSnapshot>> ListChannelVideosAsync(
        string accessToken,
        string channelId,
        int maxResults = 25,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<YouTubeVideoSnapshot>> GetVideoStatisticsAsync(
        string accessToken,
        IReadOnlyList<string> videoIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<YouTubeCommentSnapshot>> ListVideoCommentsAsync(
        string accessToken,
        string videoId,
        int maxResults = 50,
        CancellationToken cancellationToken = default);
}

public sealed class YouTubeVideoSnapshot
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime? PublishedAt { get; set; }
    public long ViewCount { get; set; }
    public long LikeCount { get; set; }
    public long CommentCount { get; set; }
    public string? Permalink { get; set; }
}

public sealed class YouTubeCommentSnapshot
{
    public string CommentId { get; set; } = string.Empty;
    public string VideoId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorChannelId { get; set; }
    public string Message { get; set; } = string.Empty;
    public long LikeCount { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? ParentCommentId { get; set; }
}
