using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SocialMedia.Application.YouTube;

namespace SocialMedia.Infrastructure.YouTube;

public sealed class YouTubeApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<YouTubeApiClient> _logger;

    public YouTubeApiClient(HttpClient http, ILogger<YouTubeApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<JsonDocument> ExchangeAuthorizationCodeAsync(
        string clientId,
        string clientSecret,
        string redirectUri,
        string code,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        using var response = await _http.PostAsync("https://oauth2.googleapis.com/token", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Google OAuth token exchange failed ({(int)response.StatusCode}): {body}");

        return JsonDocument.Parse(body);
    }

    public async Task<JsonDocument> GetAsync(
        string accessToken,
        string path,
        IReadOnlyDictionary<string, string?> query,
        CancellationToken cancellationToken)
    {
        var queryString = string.Join("&",
            query.Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));
        var url = $"https://www.googleapis.com/youtube/v3/{path.TrimStart('/')}" +
                  (string.IsNullOrWhiteSpace(queryString) ? string.Empty : $"?{queryString}");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (YouTubeApiErrors.IsCommentsDisabled((int)response.StatusCode, body) &&
                path.Contains("commentThreads", StringComparison.OrdinalIgnoreCase))
            {
                query.TryGetValue("videoId", out var videoId);
                throw new YouTubeCommentsDisabledException(videoId ?? "unknown");
            }

            _logger.LogWarning("YouTube API GET {Path} failed ({Status}): {Body}", path, (int)response.StatusCode, body);
            throw new InvalidOperationException($"YouTube API request failed ({(int)response.StatusCode}): {body}");
        }

        return JsonDocument.Parse(body);
    }

    public async Task<JsonDocument> PostAsync(
        string accessToken,
        string path,
        IReadOnlyDictionary<string, string?> query,
        object body,
        CancellationToken cancellationToken)
    {
        var queryString = string.Join("&",
            query.Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));
        var url = $"https://www.googleapis.com/youtube/v3/{path.TrimStart('/')}" +
                  (string.IsNullOrWhiteSpace(queryString) ? string.Empty : $"?{queryString}");

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, cancellationToken);
        var bodyText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("YouTube API POST {Path} failed ({Status}): {Body}", path, (int)response.StatusCode, bodyText);
            throw new InvalidOperationException($"YouTube API request failed ({(int)response.StatusCode}): {bodyText}");
        }

        return JsonDocument.Parse(bodyText);
    }

    public async Task<string> UploadVideoAsync(
        string accessToken,
        object snippet,
        object status,
        Stream videoStream,
        string contentType,
        CancellationToken cancellationToken)
    {
        using var initRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable&part=snippet,status");
        initRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        initRequest.Content = new StringContent(JsonSerializer.Serialize(new { snippet, status }), Encoding.UTF8, "application/json");

        using var initResponse = await _http.SendAsync(initRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var initBody = await initResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!initResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"YouTube upload init failed ({(int)initResponse.StatusCode}): {initBody}");

        var uploadUrl = initResponse.Headers.Location?.ToString()
            ?? throw new InvalidOperationException("YouTube did not return an upload URL.");

        videoStream.Position = 0;
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
        uploadRequest.Content = new StreamContent(videoStream);
        uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "video/*" : contentType);

        using var uploadResponse = await _http.SendAsync(uploadRequest, cancellationToken);
        var uploadBody = await uploadResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!uploadResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"YouTube upload failed ({(int)uploadResponse.StatusCode}): {uploadBody}");

        using var doc = JsonDocument.Parse(uploadBody);
        return doc.RootElement.TryGetProperty("id", out var idEl)
            ? idEl.GetString() ?? string.Empty
            : string.Empty;
    }
}
