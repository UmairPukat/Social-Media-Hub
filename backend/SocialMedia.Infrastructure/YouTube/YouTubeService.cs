using System.Text.Json;
using Microsoft.Extensions.Logging;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Infrastructure.YouTube;

public class YouTubeService : IYouTubeService
{
    private readonly YouTubeApiClient _api;
    private readonly ILogger<YouTubeService> _logger;

    public YouTubeService(YouTubeApiClient api, ILogger<YouTubeService> logger)
    {
        _api = api;
        _logger = logger;
    }

    public async Task<OAuthTokenResult> ExchangeAuthorizationCodeAsync(
        string clientId,
        string clientSecret,
        string redirectUri,
        string code,
        CancellationToken cancellationToken = default)
    {
        using var doc = await _api.ExchangeAuthorizationCodeAsync(
            clientId, clientSecret, redirectUri, code, cancellationToken);
        var root = doc.RootElement;
        var token = root.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Google did not return an access token.");

        DateTime? expiresAt = null;
        if (root.TryGetProperty("expires_in", out var expires) && expires.TryGetInt64(out var seconds))
            expiresAt = DateTime.UtcNow.AddSeconds(seconds);

        return new OAuthTokenResult
        {
            AccessToken = token,
            RefreshToken = root.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : token,
            ExpiresAt = expiresAt,
            TokenType = root.TryGetProperty("token_type", out var type) ? type.GetString() : "Bearer"
        };
    }

    public async Task<IReadOnlyList<SocialProfileDraft>> DiscoverChannelsAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var owned = await ListChannelsAsync(accessToken, mine: true, cancellationToken: cancellationToken);
        if (owned.Count > 0)
            return owned;

        return await ListChannelsAsync(accessToken, managedByMe: true, cancellationToken: cancellationToken);
    }

    private async Task<IReadOnlyList<SocialProfileDraft>> ListChannelsAsync(
        string accessToken,
        bool mine = false,
        bool managedByMe = false,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>
        {
            ["part"] = "snippet,contentDetails,statistics",
            ["maxResults"] = "50"
        };

        if (mine)
            query["mine"] = "true";
        if (managedByMe)
            query["managedByMe"] = "true";

        using var doc = await _api.GetAsync(accessToken, "channels", query, cancellationToken);

        if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return Array.Empty<SocialProfileDraft>();

        var drafts = new List<SocialProfileDraft>();
        foreach (var item in items.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id)) continue;

            var snippet = item.TryGetProperty("snippet", out var snippetEl) ? snippetEl : default;
            var title = snippet.ValueKind == JsonValueKind.Object && snippet.TryGetProperty("title", out var titleEl)
                ? titleEl.GetString()
                : null;
            var customUrl = snippet.ValueKind == JsonValueKind.Object && snippet.TryGetProperty("customUrl", out var customEl)
                ? customEl.GetString()
                : null;
            var thumb = ReadBestThumbnail(snippet);

            drafts.Add(new SocialProfileDraft
            {
                ExternalProfileId = id,
                Name = title ?? "YouTube Channel",
                Username = customUrl,
                ProfileImage = thumb,
                ProfileType = "YouTubeChannel"
            });
        }

        return drafts;
    }

    public async Task<IReadOnlyList<YouTubeVideoSnapshot>> ListChannelVideosAsync(
        string accessToken,
        string channelId,
        int maxResults = 25,
        CancellationToken cancellationToken = default)
    {
        var uploadsPlaylistId = await ResolveUploadsPlaylistIdAsync(accessToken, channelId, cancellationToken);
        if (string.IsNullOrWhiteSpace(uploadsPlaylistId))
            return Array.Empty<YouTubeVideoSnapshot>();

        using var playlistDoc = await _api.GetAsync(accessToken, "playlistItems", new Dictionary<string, string?>
        {
            ["part"] = "snippet,contentDetails",
            ["playlistId"] = uploadsPlaylistId,
            ["maxResults"] = Math.Clamp(maxResults, 1, 50).ToString()
        }, cancellationToken);

        var videoIds = new List<string>();
        var snippets = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (playlistDoc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                var videoId = item.TryGetProperty("contentDetails", out var details) &&
                                details.TryGetProperty("videoId", out var videoIdEl)
                    ? videoIdEl.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(videoId)) continue;
                videoIds.Add(videoId);
                if (item.TryGetProperty("snippet", out var snippet))
                    snippets[videoId] = snippet;
            }
        }

        if (videoIds.Count == 0)
            return Array.Empty<YouTubeVideoSnapshot>();

        var stats = await GetVideoStatisticsAsync(accessToken, videoIds, cancellationToken);
        foreach (var video in stats)
        {
            if (snippets.TryGetValue(video.VideoId, out var snippet))
            {
                video.Title = ReadString(snippet, "title") ?? video.Title;
                video.Description = ReadString(snippet, "description");
                video.ThumbnailUrl ??= ReadBestThumbnail(snippet);
                video.PublishedAt ??= ReadDate(snippet, "publishedAt");
            }
        }

        return stats;
    }

    public async Task<IReadOnlyList<YouTubeVideoSnapshot>> GetVideoStatisticsAsync(
        string accessToken,
        IReadOnlyList<string> videoIds,
        CancellationToken cancellationToken = default)
    {
        if (videoIds.Count == 0)
            return Array.Empty<YouTubeVideoSnapshot>();

        using var doc = await _api.GetAsync(accessToken, "videos", new Dictionary<string, string?>
        {
            ["part"] = "snippet,statistics,contentDetails",
            ["id"] = string.Join(",", videoIds.Distinct())
        }, cancellationToken);

        if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return Array.Empty<YouTubeVideoSnapshot>();

        var result = new List<YouTubeVideoSnapshot>();
        foreach (var item in items.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id)) continue;

            var snippet = item.TryGetProperty("snippet", out var snippetEl) ? snippetEl : default;
            var statistics = item.TryGetProperty("statistics", out var statsEl) ? statsEl : default;
            result.Add(new YouTubeVideoSnapshot
            {
                VideoId = id,
                Title = ReadString(snippet, "title") ?? id,
                Description = ReadString(snippet, "description"),
                ThumbnailUrl = ReadBestThumbnail(snippet),
                PublishedAt = ReadDate(snippet, "publishedAt"),
                ViewCount = ReadLong(statistics, "viewCount"),
                LikeCount = ReadLong(statistics, "likeCount"),
                CommentCount = ReadLong(statistics, "commentCount"),
                Permalink = $"https://www.youtube.com/watch?v={id}"
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<YouTubeCommentSnapshot>> ListVideoCommentsAsync(
        string accessToken,
        string videoId,
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        using var doc = await _api.GetAsync(accessToken, "commentThreads", new Dictionary<string, string?>
        {
            ["part"] = "snippet,replies",
            ["videoId"] = videoId,
            ["maxResults"] = Math.Clamp(maxResults, 1, 100).ToString(),
            ["order"] = "time",
            ["textFormat"] = "plainText"
        }, cancellationToken);

        if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return Array.Empty<YouTubeCommentSnapshot>();

        var comments = new List<YouTubeCommentSnapshot>();
        foreach (var thread in items.EnumerateArray())
        {
            if (!thread.TryGetProperty("snippet", out var threadSnippet)) continue;
            if (!threadSnippet.TryGetProperty("topLevelComment", out var topLevel)) continue;
            if (!topLevel.TryGetProperty("snippet", out var snippet)) continue;

            var topId = topLevel.TryGetProperty("id", out var topIdEl) ? topIdEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(topId)) continue;

            comments.Add(MapComment(topId, videoId, snippet, null));

            if (thread.TryGetProperty("replies", out var replies) &&
                replies.TryGetProperty("comments", out var replyItems) &&
                replyItems.ValueKind == JsonValueKind.Array)
            {
                foreach (var reply in replyItems.EnumerateArray())
                {
                    var replyId = reply.TryGetProperty("id", out var replyIdEl) ? replyIdEl.GetString() : null;
                    if (string.IsNullOrWhiteSpace(replyId) ||
                        !reply.TryGetProperty("snippet", out var replySnippet))
                        continue;

                    comments.Add(MapComment(replyId, videoId, replySnippet, topId));
                }
            }
        }

        return comments;
    }

    private async Task<string?> ResolveUploadsPlaylistIdAsync(
        string accessToken,
        string channelId,
        CancellationToken cancellationToken)
    {
        using var doc = await _api.GetAsync(accessToken, "channels", new Dictionary<string, string?>
        {
            ["part"] = "contentDetails",
            ["id"] = channelId
        }, cancellationToken);

        if (!doc.RootElement.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("contentDetails", out var details) &&
                details.TryGetProperty("relatedPlaylists", out var playlists) &&
                playlists.TryGetProperty("uploads", out var uploads))
                return uploads.GetString();
        }

        return null;
    }

    private static YouTubeCommentSnapshot MapComment(
        string commentId,
        string videoId,
        JsonElement snippet,
        string? parentCommentId)
        => new()
        {
            CommentId = commentId,
            VideoId = videoId,
            AuthorName = ReadString(snippet, "authorDisplayName") ?? "YouTube user",
            AuthorChannelId = ReadString(snippet, "authorChannelId"),
            Message = ReadString(snippet, "textDisplay") ?? string.Empty,
            LikeCount = ReadLong(snippet, "likeCount"),
            PublishedAt = ReadDate(snippet, "publishedAt"),
            ParentCommentId = parentCommentId
        };

    private static string? ReadString(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            ? value.GetString()
            : null;

    private static long ReadLong(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
            return 0;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            return number;

        return long.TryParse(value.GetString(), out var parsed) ? parsed : 0;
    }

    private static DateTime? ReadDate(JsonElement element, string property)
    {
        var raw = ReadString(element, property);
        return DateTime.TryParse(raw, out var parsed) ? parsed.ToUniversalTime() : null;
    }

    private static string? ReadBestThumbnail(JsonElement snippet)
    {
        if (snippet.ValueKind != JsonValueKind.Object ||
            !snippet.TryGetProperty("thumbnails", out var thumbnails) ||
            thumbnails.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var key in new[] { "maxres", "standard", "high", "medium", "default" })
        {
            if (thumbnails.TryGetProperty(key, out var thumb) &&
                thumb.TryGetProperty("url", out var url))
            {
                var value = url.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }

        return null;
    }
}
