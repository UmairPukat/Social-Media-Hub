using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SocialMedia.Infrastructure.TikTok;

public sealed class TikTokApiClient
{
    public const string BasicUserInfoFields = "open_id,union_id,avatar_url,avatar_large_url,display_name";
    public const string VideoListFields =
        "id,title,video_description,cover_image_url,share_url,create_time,view_count,like_count,comment_count,share_count";

    private readonly HttpClient _http;
    private readonly ILogger<TikTokApiClient> _logger;

    public TikTokApiClient(HttpClient http, ILogger<TikTokApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<JsonDocument> ExchangeAuthorizationCodeAsync(
        string clientKey,
        string clientSecret,
        string redirectUri,
        string code,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_key"] = clientKey,
            ["client_secret"] = clientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://open.tiktokapis.com/v2/oauth/token/")
        {
            Content = content
        };
        request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (TryReadOAuthError(root, out var errorMessage))
        {
            doc.Dispose();
            throw new InvalidOperationException(errorMessage);
        }

        if (!response.IsSuccessStatusCode)
        {
            doc.Dispose();
            throw new InvalidOperationException($"TikTok OAuth token exchange failed ({(int)response.StatusCode}): {body}");
        }

        return doc;
    }

    public async Task<JsonDocument> GetUserInfoAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var url = $"https://open.tiktokapis.com/v2/user/info/?fields={Uri.EscapeDataString(BasicUserInfoFields)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (TryReadApiError(root, out var errorMessage))
        {
            doc.Dispose();
            _logger.LogWarning("TikTok user info API error: {Message}. Body: {Body}", errorMessage, body);
            throw new InvalidOperationException(errorMessage);
        }

        if (!response.IsSuccessStatusCode)
        {
            doc.Dispose();
            _logger.LogWarning("TikTok user info failed ({Status}): {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"TikTok user info request failed ({(int)response.StatusCode}): {body}");
        }

        return doc;
    }

    public async Task<JsonDocument> ListVideosAsync(
        string accessToken,
        long? cursor = null,
        int maxCount = 20,
        CancellationToken cancellationToken = default)
    {
        var url =
            $"https://open.tiktokapis.com/v2/video/list/?fields={Uri.EscapeDataString(VideoListFields)}";

        var payload = new Dictionary<string, object?>
        {
            ["max_count"] = Math.Clamp(maxCount, 1, 20)
        };
        if (cursor.HasValue)
            payload["cursor"] = cursor.Value;

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (TryReadApiError(root, out var errorMessage))
        {
            doc.Dispose();
            throw new InvalidOperationException(errorMessage);
        }

        if (!response.IsSuccessStatusCode)
        {
            doc.Dispose();
            throw new InvalidOperationException($"TikTok video list request failed ({(int)response.StatusCode}): {body}");
        }

        return doc;
    }

    public async Task<JsonDocument> QueryVideosAsync(
        string accessToken,
        IReadOnlyList<string> videoIds,
        CancellationToken cancellationToken = default)
    {
        var url =
            $"https://open.tiktokapis.com/v2/video/query/?fields={Uri.EscapeDataString(VideoListFields)}";

        var payload = new Dictionary<string, object?>
        {
            ["filters"] = new Dictionary<string, object?>
            {
                ["video_ids"] = videoIds
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (TryReadApiError(root, out var errorMessage))
        {
            doc.Dispose();
            throw new InvalidOperationException(errorMessage);
        }

        if (!response.IsSuccessStatusCode)
        {
            doc.Dispose();
            throw new InvalidOperationException($"TikTok video query request failed ({(int)response.StatusCode}): {body}");
        }

        return doc;
    }

    public async Task<JsonDocument> QueryCreatorInfoAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://open.tiktokapis.com/v2/post/publish/creator_info/query/")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (TryReadApiError(root, out var errorMessage))
        {
            doc.Dispose();
            throw new InvalidOperationException(errorMessage);
        }

        if (!response.IsSuccessStatusCode)
        {
            doc.Dispose();
            throw new InvalidOperationException($"TikTok creator info failed ({(int)response.StatusCode}): {body}");
        }

        return doc;
    }

    public async Task<(string PublishId, string UploadUrl)> InitDirectVideoPublishAsync(
        string accessToken,
        string title,
        string privacyLevel,
        bool disableComment,
        bool disableDuet,
        bool disableStitch,
        bool brandContentToggle,
        bool brandOrganicToggle,
        long videoSize,
        long chunkSize,
        int totalChunkCount,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object>
        {
            ["post_info"] = new Dictionary<string, object>
            {
                ["title"] = title,
                ["privacy_level"] = privacyLevel,
                ["disable_comment"] = disableComment,
                ["disable_duet"] = disableDuet,
                ["disable_stitch"] = disableStitch,
                ["brand_content_toggle"] = brandContentToggle,
                ["brand_organic_toggle"] = brandOrganicToggle
            },
            ["source_info"] = new Dictionary<string, object>
            {
                ["source"] = "FILE_UPLOAD",
                ["video_size"] = videoSize,
                ["chunk_size"] = chunkSize,
                ["total_chunk_count"] = totalChunkCount
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://open.tiktokapis.com/v2/post/publish/video/init/")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (TryReadApiError(root, out var errorMessage))
            throw new InvalidOperationException(errorMessage);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"TikTok video publish init failed ({(int)response.StatusCode}): {body}");

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("TikTok did not return publish init data.");

        var publishId = data.TryGetProperty("publish_id", out var publishEl) ? publishEl.GetString() : null;
        var uploadUrl = data.TryGetProperty("upload_url", out var uploadEl) ? uploadEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(publishId) || string.IsNullOrWhiteSpace(uploadUrl))
            throw new InvalidOperationException("TikTok did not return publish_id or upload_url.");

        return (publishId, uploadUrl);
    }

    public async Task<string> InitDirectPhotoPublishAsync(
        string accessToken,
        string title,
        string description,
        string privacyLevel,
        bool disableComment,
        bool brandContentToggle,
        bool brandOrganicToggle,
        bool autoAddMusic,
        IReadOnlyList<string> photoUrls,
        int photoCoverIndex,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object>
        {
            ["post_info"] = new Dictionary<string, object>
            {
                ["title"] = title,
                ["description"] = description,
                ["privacy_level"] = privacyLevel,
                ["disable_comment"] = disableComment,
                ["brand_content_toggle"] = brandContentToggle,
                ["brand_organic_toggle"] = brandOrganicToggle,
                ["auto_add_music"] = autoAddMusic
            },
            ["source_info"] = new Dictionary<string, object>
            {
                ["source"] = "PULL_FROM_URL",
                ["photo_cover_index"] = photoCoverIndex,
                ["photo_images"] = photoUrls
            },
            ["post_mode"] = "DIRECT_POST",
            ["media_type"] = "PHOTO"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://open.tiktokapis.com/v2/post/publish/content/init/")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (TryReadApiError(root, out var errorMessage))
            throw new InvalidOperationException(errorMessage);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"TikTok photo publish init failed ({(int)response.StatusCode}): {body}");

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("TikTok did not return photo publish init data.");

        var publishId = data.TryGetProperty("publish_id", out var publishEl) ? publishEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(publishId))
            throw new InvalidOperationException("TikTok did not return publish_id.");

        return publishId;
    }

    public async Task UploadVideoChunkAsync(
        string uploadUrl,
        byte[] buffer,
        int offset,
        int count,
        long totalSize,
        CancellationToken cancellationToken)
    {
        var start = offset;
        var end = offset + count - 1;
        using var content = new ByteArrayContent(buffer, 0, count);
        content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        content.Headers.ContentLength = count;

        using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl) { Content = content };
        request.Headers.TryAddWithoutValidation("Content-Range", $"bytes {start}-{end}/{totalSize}");

        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"TikTok video upload failed ({(int)response.StatusCode}): {body}");
    }

    public async Task<JsonDocument> FetchPublishStatusAsync(
        string accessToken,
        string publishId,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new Dictionary<string, string> { ["publish_id"] = publishId });
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://open.tiktokapis.com/v2/post/publish/status/fetch/")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (TryReadApiError(root, out var errorMessage))
        {
            doc.Dispose();
            throw new InvalidOperationException(errorMessage);
        }

        if (!response.IsSuccessStatusCode)
        {
            doc.Dispose();
            throw new InvalidOperationException($"TikTok publish status failed ({(int)response.StatusCode}): {body}");
        }

        return doc;
    }

    private static bool TryReadOAuthError(JsonElement root, out string message)
    {
        message = string.Empty;

        if (!root.TryGetProperty("error", out var errorEl))
            return false;

        if (errorEl.ValueKind == JsonValueKind.String)
        {
            var code = errorEl.GetString();
            var description = root.TryGetProperty("error_description", out var descEl)
                ? descEl.GetString()
                : null;
            message = string.IsNullOrWhiteSpace(description)
                ? $"TikTok OAuth error: {code}"
                : $"TikTok OAuth error: {description}";
            return true;
        }

        if (errorEl.ValueKind == JsonValueKind.Object)
        {
            var code = errorEl.TryGetProperty("code", out var codeEl) ? codeEl.GetString() : null;
            var description = errorEl.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : null;
            message = string.IsNullOrWhiteSpace(description)
                ? $"TikTok OAuth error: {code ?? "unknown"}"
                : $"TikTok OAuth error: {description}";
            return true;
        }

        return false;
    }

    /// <summary>
    /// TikTok business APIs return { data, error:{ code, message } } where code == "ok" means success.
    /// </summary>
    public static bool TryReadApiError(JsonElement root, out string message)
    {
        message = string.Empty;

        if (!root.TryGetProperty("error", out var errorEl) || errorEl.ValueKind != JsonValueKind.Object)
            return false;

        var code = errorEl.TryGetProperty("code", out var codeEl) ? codeEl.GetString() : null;
        if (string.Equals(code, "ok", StringComparison.OrdinalIgnoreCase))
            return false;

        var description = errorEl.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : null;
        var logId = errorEl.TryGetProperty("log_id", out var logEl) ? logEl.GetString() : null;
        message = string.IsNullOrWhiteSpace(description)
            ? $"TikTok API error: {code ?? "unknown"}"
            : $"TikTok API error: {description}";
        if (!string.IsNullOrWhiteSpace(logId))
            message += $" (log_id: {logId})";
        return true;
    }
}
