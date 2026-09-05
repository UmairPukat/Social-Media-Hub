using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.DTOs.TikTok;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Infrastructure.TikTok;

public class TikTokService : ITikTokService
{
    private readonly TikTokApiClient _api;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TikTokService> _logger;

    public TikTokService(
        TikTokApiClient api,
        IConfiguration configuration,
        ILogger<TikTokService> logger)
    {
        _api = api;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<OAuthTokenResult> ExchangeAuthorizationCodeAsync(
        string clientKey,
        string clientSecret,
        string redirectUri,
        string code,
        string codeVerifier,
        CancellationToken cancellationToken = default)
    {
        using var doc = await _api.ExchangeAuthorizationCodeAsync(
            clientKey, clientSecret, redirectUri, code, codeVerifier, cancellationToken);
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

    public async Task<TikTokPublishResult> PublishVideoAsync(
        string accessToken,
        Stream videoStream,
        long videoSize,
        string contentType,
        TikTokPublishOptions options,
        CancellationToken cancellationToken = default)
    {
        if (videoSize <= 0)
            throw new InvalidOperationException("TikTok requires a non-empty video file.");

        if (!IsVideoContentType(contentType))
            throw new InvalidOperationException("TikTok direct publish requires a video file (MP4, MOV, or WebM).");

        var prepared = await PreparePublishOptionsAsync(accessToken, options, isPhoto: false, cancellationToken);

        const long maxChunkSize = 10 * 1024 * 1024;
        var chunkSize = videoSize <= maxChunkSize ? videoSize : maxChunkSize;
        var totalChunkCount = (int)Math.Ceiling((double)videoSize / chunkSize);

        var title = Truncate(prepared.Title, 2200);

        var (publishId, uploadUrl) = await InitVideoPublishAsync(
            accessToken,
            title,
            prepared,
            videoSize,
            chunkSize,
            totalChunkCount,
            cancellationToken);

        await UploadVideoInChunksAsync(videoStream, uploadUrl, videoSize, (int)chunkSize, cancellationToken);

        var videoId = await PollPublishStatusAsync(accessToken, publishId, cancellationToken);
        return new TikTokPublishResult
        {
            PublishId = publishId,
            VideoId = videoId
        };
    }

    public async Task<TikTokPublishResult> PublishPhotoAsync(
        string accessToken,
        IReadOnlyList<string> photoUrls,
        TikTokPublishOptions options,
        int photoCoverIndex = 0,
        CancellationToken cancellationToken = default)
    {
        var urls = photoUrls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url.Trim())
            .Where(url => Uri.TryCreate(url, UriKind.Absolute, out var uri)
                          && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            .ToList();

        if (urls.Count == 0)
        {
            throw new InvalidOperationException(
                "TikTok photo posts require publicly accessible HTTPS image URLs. " +
                "Upload an image file or provide a verified public URL.");
        }

        if (photoCoverIndex < 0 || photoCoverIndex >= urls.Count)
            photoCoverIndex = 0;

        var prepared = await PreparePublishOptionsAsync(accessToken, options, isPhoto: true, cancellationToken);
        var title = Truncate(prepared.Title, 90);
        var description = Truncate(string.IsNullOrWhiteSpace(prepared.Description) ? prepared.Title : prepared.Description, 4000);

        var publishId = await InitPhotoPublishAsync(
            accessToken,
            title,
            description,
            prepared,
            urls,
            photoCoverIndex,
            cancellationToken);

        var postId = await PollPublishStatusAsync(accessToken, publishId, cancellationToken);
        return new TikTokPublishResult
        {
            PublishId = publishId,
            VideoId = postId
        };
    }

    public async Task<string> StagePublishMediaAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "publish-cache");
        Directory.CreateDirectory(cacheDir);

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = contentType.Trim().ToLowerInvariant() switch
            {
                "image/jpeg" or "image/jpg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => ".jpg"
            };
        }

        var storedName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(cacheDir, storedName);
        await using (var output = File.Create(fullPath))
        {
            await stream.CopyToAsync(output, cancellationToken);
        }

        var baseUrl = ResolvePublishMediaBaseUrl();
        return $"{baseUrl}/publish-cache/{storedName}";
    }

    private async Task<TikTokPublishOptions> PreparePublishOptionsAsync(
        string accessToken,
        TikTokPublishOptions options,
        bool isPhoto,
        CancellationToken cancellationToken)
    {
        using var doc = await _api.QueryCreatorInfoAsync(accessToken, cancellationToken);
        var creator = ParseCreatorInfo(doc.RootElement);
        var prepared = new TikTokPublishOptions
        {
            Title = options.Title,
            Description = options.Description,
            PrivacyLevel = ResolvePrivacyLevel(options.PrivacyLevel, creator.PrivacyLevelOptions),
            DisableComment = options.DisableComment || creator.CommentDisabled,
            DisableDuet = isPhoto || options.DisableDuet || creator.DuetDisabled,
            DisableStitch = isPhoto || options.DisableStitch || creator.StitchDisabled,
            BrandContentToggle = options.BrandContentToggle,
            BrandOrganicToggle = options.BrandOrganicToggle,
            AutoAddMusic = options.AutoAddMusic
        };

        ApplySandboxPrivacyRules(prepared, options.PrivacyLevel);
        return prepared;
    }

    private void ApplySandboxPrivacyRules(TikTokPublishOptions prepared, string requestedPrivacy)
    {
        if (!IsSandboxMode())
            return;

        if (!string.Equals(prepared.PrivacyLevel, "SELF_ONLY", StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "TikTok sandbox mode: using SELF_ONLY instead of {Privacy} for Direct Post testing.",
                prepared.PrivacyLevel);
        }

        prepared.PrivacyLevel = "SELF_ONLY";
    }

    private bool IsSandboxMode()
    {
        var configured = _configuration["TikTokSettings:SandboxMode"];
        if (string.IsNullOrWhiteSpace(configured))
            return true;

        return !string.Equals(configured.Trim(), "false", StringComparison.OrdinalIgnoreCase)
               && !string.Equals(configured.Trim(), "0", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(string PublishId, string UploadUrl)> InitVideoPublishAsync(
        string accessToken,
        string title,
        TikTokPublishOptions prepared,
        long videoSize,
        long chunkSize,
        int totalChunkCount,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _api.InitDirectVideoPublishAsync(
                accessToken,
                title,
                prepared.PrivacyLevel,
                prepared.DisableComment,
                prepared.DisableDuet,
                prepared.DisableStitch,
                prepared.BrandContentToggle,
                prepared.BrandOrganicToggle,
                videoSize,
                chunkSize,
                totalChunkCount,
                cancellationToken);
        }
        catch (InvalidOperationException ex) when (IsSandboxMode() && IsUnauditedDirectPostError(ex))
        {
            if (string.Equals(prepared.PrivacyLevel, "SELF_ONLY", StringComparison.Ordinal))
                throw;

            _logger.LogWarning(ex, "TikTok sandbox publish retrying with SELF_ONLY.");
            prepared.PrivacyLevel = "SELF_ONLY";
            return await _api.InitDirectVideoPublishAsync(
                accessToken,
                title,
                prepared.PrivacyLevel,
                prepared.DisableComment,
                prepared.DisableDuet,
                prepared.DisableStitch,
                prepared.BrandContentToggle,
                prepared.BrandOrganicToggle,
                videoSize,
                chunkSize,
                totalChunkCount,
                cancellationToken);
        }
    }

    private async Task<string> InitPhotoPublishAsync(
        string accessToken,
        string title,
        string description,
        TikTokPublishOptions prepared,
        IReadOnlyList<string> photoUrls,
        int photoCoverIndex,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _api.InitDirectPhotoPublishAsync(
                accessToken,
                title,
                description,
                prepared.PrivacyLevel,
                prepared.DisableComment,
                prepared.BrandContentToggle,
                prepared.BrandOrganicToggle,
                prepared.AutoAddMusic,
                photoUrls,
                photoCoverIndex,
                cancellationToken);
        }
        catch (InvalidOperationException ex) when (IsSandboxMode() && IsUnauditedDirectPostError(ex))
        {
            if (string.Equals(prepared.PrivacyLevel, "SELF_ONLY", StringComparison.Ordinal))
                throw;

            _logger.LogWarning(ex, "TikTok sandbox photo publish retrying with SELF_ONLY.");
            prepared.PrivacyLevel = "SELF_ONLY";
            return await _api.InitDirectPhotoPublishAsync(
                accessToken,
                title,
                description,
                prepared.PrivacyLevel,
                prepared.DisableComment,
                prepared.BrandContentToggle,
                prepared.BrandOrganicToggle,
                prepared.AutoAddMusic,
                photoUrls,
                photoCoverIndex,
                cancellationToken);
        }
    }

    private static bool IsUnauditedDirectPostError(InvalidOperationException ex)
        => ex.Message.Contains("unaudited_client_can_only_post_to_private_accounts", StringComparison.OrdinalIgnoreCase);

    private static TikTokCreatorInfo ParseCreatorInfo(JsonElement root)
    {
        var info = new TikTokCreatorInfo();
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return info;

        if (data.TryGetProperty("privacy_level_options", out var privacyEl) &&
            privacyEl.ValueKind == JsonValueKind.Array)
        {
            info.PrivacyLevelOptions = privacyEl.EnumerateArray()
                .Select(item => item.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToList();
        }

        info.CommentDisabled = ReadBool(data, "comment_disabled");
        info.DuetDisabled = ReadBool(data, "duet_disabled");
        info.StitchDisabled = ReadBool(data, "stitch_disabled");
        if (data.TryGetProperty("max_video_post_duration_sec", out var durationEl) &&
            durationEl.TryGetInt32(out var durationSec))
        {
            info.MaxVideoPostDurationSec = durationSec;
        }

        return info;
    }

    private static string ResolvePrivacyLevel(string requested, IReadOnlyList<string> allowedOptions)
    {
        if (!string.IsNullOrWhiteSpace(requested) &&
            allowedOptions.Count > 0 &&
            allowedOptions.Contains(requested, StringComparer.Ordinal))
        {
            return requested;
        }

        if (!string.IsNullOrWhiteSpace(requested))
            return requested;

        if (allowedOptions.Count > 0)
            return allowedOptions[0];

        return "PUBLIC_TO_EVERYONE";
    }

    private static bool ReadBool(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var valueEl) &&
           valueEl.ValueKind == JsonValueKind.True;

    private string ResolvePublishMediaBaseUrl()
    {
        var configured = _configuration["TikTokSettings:PublishMediaBaseUrl"]
                           ?? _configuration["BackendBaseUrl"];
        var baseUrl = configured?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                "BackendBaseUrl (or TikTokSettings:PublishMediaBaseUrl) must be configured so TikTok can pull photo URLs. " +
                "Verify the URL prefix in your TikTok developer app under Content Posting → URL ownership.");
        }

        return baseUrl;
    }

    private static string Truncate(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private async Task UploadVideoInChunksAsync(
        Stream videoStream,
        string uploadUrl,
        long videoSize,
        int chunkSize,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[chunkSize];
        long uploaded = 0;

        while (uploaded < videoSize)
        {
            var toRead = (int)Math.Min(chunkSize, videoSize - uploaded);
            var read = 0;
            while (read < toRead)
            {
                var n = await videoStream.ReadAsync(buffer.AsMemory(read, toRead - read), cancellationToken);
                if (n == 0)
                    throw new InvalidOperationException("Video stream ended before upload completed.");
                read += n;
            }

            await _api.UploadVideoChunkAsync(uploadUrl, buffer, (int)uploaded, read, videoSize, cancellationToken);
            uploaded += read;
        }
    }

    private async Task<string?> PollPublishStatusAsync(
        string accessToken,
        string publishId,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 100;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            using var doc = await _api.FetchPublishStatusAsync(accessToken, publishId, cancellationToken);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("TikTok did not return publish status data.");

            var status = data.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;
            if (string.Equals(status, "FAILED", StringComparison.OrdinalIgnoreCase))
            {
                var reason = data.TryGetProperty("fail_reason", out var reasonEl) ? reasonEl.GetString() : null;
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(reason)
                        ? "TikTok publish failed."
                        : $"TikTok publish failed: {reason}");
            }

            if (string.Equals(status, "PUBLISH_COMPLETE", StringComparison.OrdinalIgnoreCase))
            {
                if (data.TryGetProperty("publicaly_available_post_id", out var postIdsEl) &&
                    postIdsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in postIdsEl.EnumerateArray())
                    {
                        var id = item.GetString();
                        if (!string.IsNullOrWhiteSpace(id))
                            return id;
                    }
                }

                return publishId;
            }

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }

        throw new InvalidOperationException("TikTok publish timed out while waiting for completion.");
    }

    private static bool IsVideoContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        var lower = contentType.Trim().ToLowerInvariant();
        return lower.StartsWith("video/", StringComparison.Ordinal) ||
               lower is "application/octet-stream" or "binary/octet-stream";
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
