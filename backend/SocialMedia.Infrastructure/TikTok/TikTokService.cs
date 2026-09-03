using System.Text.Json;
using Microsoft.Extensions.Logging;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Infrastructure.TikTok;

public class TikTokService : ITikTokService
{
    private readonly TikTokApiClient _api;
    private readonly ILogger<TikTokService> _logger;

    public TikTokService(TikTokApiClient api, ILogger<TikTokService> logger)
    {
        _api = api;
        _logger = logger;
    }

    public async Task<OAuthTokenResult> ExchangeAuthorizationCodeAsync(
        string clientKey,
        string clientSecret,
        string redirectUri,
        string code,
        CancellationToken cancellationToken = default)
    {
        using var doc = await _api.ExchangeAuthorizationCodeAsync(
            clientKey, clientSecret, redirectUri, code, cancellationToken);
        var root = doc.RootElement;

        // Token endpoint returns fields at the root — not under "data".
        var tokenPayload = root.TryGetProperty("data", out var wrapped) &&
                           wrapped.ValueKind == JsonValueKind.Object &&
                           wrapped.TryGetProperty("access_token", out _)
            ? wrapped
            : root;

        var token = tokenPayload.TryGetProperty("access_token", out var accessEl) ? accessEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("TikTok did not return an access token.");

        DateTime? expiresAt = null;
        if (tokenPayload.TryGetProperty("expires_in", out var expires) && expires.TryGetInt64(out var seconds))
            expiresAt = DateTime.UtcNow.AddSeconds(seconds);

        var openId = tokenPayload.TryGetProperty("open_id", out var openEl) ? openEl.GetString() : null;
        var scope = tokenPayload.TryGetProperty("scope", out var scopeEl) ? scopeEl.GetString() : null;

        return new OAuthTokenResult
        {
            AccessToken = token,
            RefreshToken = tokenPayload.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : token,
            ExpiresAt = expiresAt,
            TokenType = tokenPayload.TryGetProperty("token_type", out var type) ? type.GetString() : "Bearer",
            OpenId = openId,
            Scope = scope
        };
    }

    public async Task<SocialProfileDraft?> ResolveProfileAsync(
        OAuthTokenResult token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token.AccessToken))
            return null;

        try
        {
            using var doc = await _api.GetUserInfoAsync(token.AccessToken, cancellationToken);
            var profile = ParseUserInfoProfile(doc.RootElement);
            if (profile is not null)
                return profile;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TikTok user info lookup failed; falling back to token open_id if available.");
        }

        return BuildFallbackProfile(token.OpenId, token.Scope);
    }

    public async Task<IReadOnlyList<SocialProfileDraft>> DiscoverProfilesAsync(
        string accessToken,
        string? openIdFallback = null,
        CancellationToken cancellationToken = default)
    {
        var token = new OAuthTokenResult
        {
            AccessToken = accessToken,
            OpenId = openIdFallback
        };
        var profile = await ResolveProfileAsync(token, cancellationToken);
        return profile is null ? Array.Empty<SocialProfileDraft>() : [profile];
    }

    public async Task<IReadOnlyList<TikTokVideoSnapshot>> ListVideosAsync(
        string accessToken,
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        var videos = new List<TikTokVideoSnapshot>();
        long? cursor = null;
        var remaining = Math.Max(1, maxResults);

        while (remaining > 0)
        {
            var pageSize = Math.Min(remaining, 20);
            using var doc = await _api.ListVideosAsync(accessToken, cursor, pageSize, cancellationToken);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                break;

            if (data.TryGetProperty("videos", out var videosEl) && videosEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in videosEl.EnumerateArray())
                {
                    var snapshot = ParseVideoSnapshot(item);
                    if (snapshot is not null)
                        videos.Add(snapshot);
                }
            }

            var hasMore = data.TryGetProperty("has_more", out var hasMoreEl) &&
                          hasMoreEl.ValueKind == JsonValueKind.True;
            if (!hasMore)
                break;

            if (!data.TryGetProperty("cursor", out var cursorEl) || !cursorEl.TryGetInt64(out var nextCursor))
                break;

            cursor = nextCursor;
            remaining -= pageSize;
        }

        return videos;
    }

    public async Task<IReadOnlyList<TikTokVideoSnapshot>> QueryVideosAsync(
        string accessToken,
        IReadOnlyList<string> videoIds,
        CancellationToken cancellationToken = default)
    {
        if (videoIds.Count == 0)
            return Array.Empty<TikTokVideoSnapshot>();

        var result = new List<TikTokVideoSnapshot>();
        foreach (var batch in videoIds.Where(id => !string.IsNullOrWhiteSpace(id)).Chunk(20))
        {
            using var doc = await _api.QueryVideosAsync(accessToken, batch.ToList(), cancellationToken);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("videos", out var videosEl) ||
                videosEl.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in videosEl.EnumerateArray())
            {
                var snapshot = ParseVideoSnapshot(item);
                if (snapshot is not null)
                    result.Add(snapshot);
            }
        }

        return result;
    }

    private static TikTokVideoSnapshot? ParseVideoSnapshot(JsonElement video)
    {
        if (!video.TryGetProperty("id", out var idEl))
            return null;

        var id = idEl.GetString();
        if (string.IsNullOrWhiteSpace(id))
            return null;

        DateTime? createTime = null;
        if (video.TryGetProperty("create_time", out var createEl) && createEl.TryGetInt64(out var unixSeconds))
            createTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;

        var title = video.TryGetProperty("title", out var titleEl) ? titleEl.GetString() : null;
        var description = video.TryGetProperty("video_description", out var descEl) ? descEl.GetString() : null;

        return new TikTokVideoSnapshot
        {
            VideoId = id,
            Title = string.IsNullOrWhiteSpace(title) ? "TikTok video" : title,
            Description = description,
            CoverImageUrl = video.TryGetProperty("cover_image_url", out var coverEl) ? coverEl.GetString() : null,
            ShareUrl = video.TryGetProperty("share_url", out var shareEl) ? shareEl.GetString() : null,
            CreateTime = createTime,
            ViewCount = ReadInt64(video, "view_count"),
            LikeCount = ReadInt64(video, "like_count"),
            CommentCount = ReadInt64(video, "comment_count"),
            ShareCount = ReadInt64(video, "share_count")
        };
    }

    private static long ReadInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var valueEl))
            return 0;

        return valueEl.ValueKind switch
        {
            JsonValueKind.Number when valueEl.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(valueEl.GetString(), out var parsed) => parsed,
            _ => 0
        };
    }

    private static SocialProfileDraft? ParseUserInfoProfile(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("user", out var user) ||
            user.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var openId = user.TryGetProperty("open_id", out var openEl) ? openEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(openId))
            return null;

        var displayName = user.TryGetProperty("display_name", out var nameEl) ? nameEl.GetString() : null;
        var avatar = user.TryGetProperty("avatar_url", out var avatarEl) ? avatarEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(avatar) &&
            user.TryGetProperty("avatar_large_url", out var largeAvatarEl))
        {
            avatar = largeAvatarEl.GetString();
        }

        return new SocialProfileDraft
        {
            ExternalProfileId = openId,
            Name = string.IsNullOrWhiteSpace(displayName) ? "TikTok Account" : displayName,
            ProfileImage = avatar,
            ProfileType = "TikTokAccount"
        };
    }

    private static SocialProfileDraft? BuildFallbackProfile(string? openId, string? scope)
    {
        if (string.IsNullOrWhiteSpace(openId))
            return null;

        return new SocialProfileDraft
        {
            ExternalProfileId = openId,
            Name = "TikTok Account",
            ProfileType = "TikTokAccount",
            Username = null
        };
    }
}
