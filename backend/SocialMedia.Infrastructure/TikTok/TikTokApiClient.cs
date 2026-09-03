using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SocialMedia.Infrastructure.TikTok;

public sealed class TikTokApiClient
{
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
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_key"] = clientKey,
            ["client_secret"] = clientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri
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

        if (TryReadTikTokError(root, out var errorMessage))
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

    private static bool TryReadTikTokError(JsonElement root, out string message)
    {
        message = string.Empty;

        if (root.TryGetProperty("error", out var errorEl))
        {
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
        }

        return false;
    }

    public async Task<JsonDocument> GetUserInfoAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://open.tiktokapis.com/v2/user/info/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(
            """{"fields":["open_id","union_id","avatar_url","display_name","username"]}""",
            Encoding.UTF8,
            "application/json");

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("TikTok user info failed ({Status}): {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"TikTok user info request failed ({(int)response.StatusCode}): {body}");
        }

        return JsonDocument.Parse(body);
    }
}
