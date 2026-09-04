using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.DTOs.TikTok;

namespace SocialMedia.Application.Interfaces;

public interface ITikTokService
{
    Task<OAuthTokenResult> ExchangeAuthorizationCodeAsync(
        string clientKey,
        string clientSecret,
        string redirectUri,
        string code,
        string codeVerifier,
        CancellationToken cancellationToken = default);

    Task<SocialProfileDraft?> ResolveProfileAsync(
        OAuthTokenResult token,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SocialProfileDraft>> DiscoverProfilesAsync(
        string accessToken,
        string? openIdFallback = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TikTokVideoSnapshot>> ListVideosAsync(
        string accessToken,
        int maxResults = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TikTokVideoSnapshot>> QueryVideosAsync(
        string accessToken,
        IReadOnlyList<string> videoIds,
        CancellationToken cancellationToken = default);

    Task<TikTokPublishResult> PublishVideoAsync(
        string accessToken,
        Stream videoStream,
        long videoSize,
        string contentType,
        TikTokPublishOptions options,
        CancellationToken cancellationToken = default);
}

public sealed class TikTokVideoSnapshot
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? ShareUrl { get; set; }
    public DateTime? CreateTime { get; set; }
    public long ViewCount { get; set; }
    public long LikeCount { get; set; }
    public long CommentCount { get; set; }
    public long ShareCount { get; set; }
}
