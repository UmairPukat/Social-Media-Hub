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

        if (!root.TryGetProperty("data", out var data))
            throw new InvalidOperationException("TikTok did not return token data.");

        var token = data.TryGetProperty("access_token", out var accessEl) ? accessEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("TikTok did not return an access token.");

        DateTime? expiresAt = null;
        if (data.TryGetProperty("expires_in", out var expires) && expires.TryGetInt64(out var seconds))
            expiresAt = DateTime.UtcNow.AddSeconds(seconds);

        return new OAuthTokenResult
        {
            AccessToken = token,
            RefreshToken = data.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : token,
            ExpiresAt = expiresAt,
            TokenType = data.TryGetProperty("token_type", out var type) ? type.GetString() : "Bearer"
        };
    }

    public async Task<IReadOnlyList<SocialProfileDraft>> DiscoverProfilesAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = await _api.GetUserInfoAsync(accessToken, cancellationToken);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("user", out var user) ||
                user.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<SocialProfileDraft>();
            }

            var openId = user.TryGetProperty("open_id", out var openEl) ? openEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(openId))
                return Array.Empty<SocialProfileDraft>();

            var displayName = user.TryGetProperty("display_name", out var nameEl) ? nameEl.GetString() : null;
            var username = user.TryGetProperty("username", out var usernameEl) ? usernameEl.GetString() : null;
            var avatar = user.TryGetProperty("avatar_url", out var avatarEl) ? avatarEl.GetString() : null;

            return
            [
                new SocialProfileDraft
                {
                    ExternalProfileId = openId,
                    Name = displayName ?? username ?? "TikTok Account",
                    Username = username,
                    ProfileImage = avatar,
                    ProfileType = "TikTokAccount"
                }
            ];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load TikTok profile after OAuth.");
            return Array.Empty<SocialProfileDraft>();
        }
    }
}
