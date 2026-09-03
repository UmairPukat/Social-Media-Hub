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
