using System.Text.Json;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Infrastructure.Meta;

public class MetaOAuthExchangeService : IMetaOAuthExchange
{
    private readonly MetaGraphClient _graph;

    public MetaOAuthExchangeService(MetaGraphClient graph)
    {
        _graph = graph;
    }

    public async Task<OAuthTokenResult> ExchangeAuthorizationCodeAsync(
        MetaOAuthCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        var platformCode = credentials.PlatformCode.Trim().ToLowerInvariant();
        if (platformCode == "instagram_login")
            return await ExchangeInstagramLoginAsync(credentials, cancellationToken);

        var host = string.IsNullOrWhiteSpace(credentials.BaseUrl)
            ? "https://graph.facebook.com"
            : credentials.BaseUrl.TrimEnd('/');

        using var shortLived = await _graph.ExchangeOAuthCodeAsync(
            host,
            credentials.GraphApiVersion,
            credentials.ClientId,
            credentials.ClientSecret,
            credentials.RedirectUri,
            credentials.Code,
            cancellationToken);

        var shortToken = shortLived.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Meta did not return an access token.");

        try
        {
            using var longLived = await _graph.ExchangeLongLivedTokenAsync(
                host,
                credentials.GraphApiVersion,
                credentials.ClientId,
                credentials.ClientSecret,
                shortToken,
                cancellationToken);
            return ParseToken(longLived.RootElement);
        }
        catch
        {
            return ParseToken(shortLived.RootElement);
        }
    }

    private async Task<OAuthTokenResult> ExchangeInstagramLoginAsync(
        MetaOAuthCredentials credentials,
        CancellationToken cancellationToken)
    {
        using var doc = await _graph.PostInstagramOAuthAsync(new Dictionary<string, string>
        {
            ["client_id"] = credentials.ClientId,
            ["client_secret"] = credentials.ClientSecret,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = credentials.RedirectUri,
            ["code"] = credentials.Code
        }, cancellationToken);

        return ParseToken(doc.RootElement);
    }

    private static OAuthTokenResult ParseToken(JsonElement root)
    {
        var token = root.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Meta did not return an access token.");

        DateTime? expiresAt = null;
        if (root.TryGetProperty("expires_in", out var expires) && expires.TryGetInt64(out var seconds))
            expiresAt = DateTime.UtcNow.AddSeconds(seconds);

        return new OAuthTokenResult
        {
            AccessToken = token,
            ExpiresAt = expiresAt
        };
    }
}
