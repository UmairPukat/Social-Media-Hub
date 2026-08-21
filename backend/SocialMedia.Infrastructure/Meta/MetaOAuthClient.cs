using System.Text.Json;
using Microsoft.Extensions.Logging;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;
using SocialMedia.Infrastructure.Meta;

namespace SocialMedia.Infrastructure.Meta;

/// <summary>
/// Exchanges OAuth codes using caller-supplied Meta app credentials (App Connections flow).
/// </summary>
public class MetaOAuthClient : IMetaOAuthClient
{
    private readonly MetaGraphClient _graph;
    private readonly ILogger<MetaOAuthClient> _logger;

    public MetaOAuthClient(MetaGraphClient graph, ILogger<MetaOAuthClient> logger)
    {
        _graph = graph;
        _logger = logger;
    }

    public async Task<OAuthTokenResult> ExchangeCodeAsync(
        string platformCode,
        MetaOAuthCredentials credentials,
        string code,
        CancellationToken cancellationToken = default)
    {
        var codeNorm = (platformCode ?? string.Empty).Trim().ToLowerInvariant();
        return codeNorm switch
        {
            "instagram_login" => await ExchangeInstagramLoginCodeAsync(credentials, code, cancellationToken),
            _ => await ExchangeFacebookDialogCodeAsync(credentials, code, cancellationToken)
        };
    }

    public async Task<(string Id, string Name)> GetMeAsync(
        string platformCode,
        string graphVersion,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var codeNorm = (platformCode ?? string.Empty).Trim().ToLowerInvariant();
        if (codeNorm == "instagram_login")
        {
            using var doc = await _graph.GetInstagramAsync(
                graphVersion, "me", accessToken, cancellationToken, ("fields", "user_id,username"));
            var id = doc.RootElement.TryGetProperty("user_id", out var uid)
                ? uid.GetString() ?? string.Empty
                : doc.RootElement.GetProperty("id").GetString() ?? string.Empty;
            var name = doc.RootElement.TryGetProperty("username", out var u)
                ? u.GetString() ?? "Instagram User"
                : "Instagram User";
            return (id, name);
        }

        using var fbDoc = await _graph.GetAsync(graphVersion, "me", accessToken, cancellationToken, ("fields", "id,name"));
        var fbId = fbDoc.RootElement.GetProperty("id").GetString() ?? string.Empty;
        var fbName = fbDoc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? "Meta User" : "Meta User";
        return (fbId, fbName);
    }

    private async Task<OAuthTokenResult> ExchangeFacebookDialogCodeAsync(
        MetaOAuthCredentials credentials,
        string code,
        CancellationToken cancellationToken)
    {
        using var shortLived = await _graph.GetAsync(
            credentials.GraphApiVersion, "oauth/access_token", string.Empty, cancellationToken,
            ("client_id", credentials.AppId),
            ("client_secret", credentials.AppSecret),
            ("redirect_uri", credentials.RedirectUri),
            ("code", code));

        var shortToken = shortLived.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Meta did not return an access token.");

        try
        {
            using var longLived = await _graph.GetAsync(
                credentials.GraphApiVersion, "oauth/access_token", string.Empty, cancellationToken,
                ("grant_type", "fb_exchange_token"),
                ("client_id", credentials.AppId),
                ("client_secret", credentials.AppSecret),
                ("fb_exchange_token", shortToken));

            return ParseToken(longLived.RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Long-lived token exchange failed; using short-lived token.");
            return ParseToken(shortLived.RootElement);
        }
    }

    private async Task<OAuthTokenResult> ExchangeInstagramLoginCodeAsync(
        MetaOAuthCredentials credentials,
        string code,
        CancellationToken cancellationToken)
    {
        using var shortLived = await _graph.PostInstagramOAuthAsync(new Dictionary<string, string>
        {
            ["client_id"] = credentials.AppId,
            ["client_secret"] = credentials.AppSecret,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = credentials.RedirectUri,
            ["code"] = code
        }, cancellationToken);

        var shortToken = shortLived.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Instagram did not return an access token.");

        try
        {
            using var longLived = await _graph.GetInstagramTokenAsync(
                "access_token",
                cancellationToken,
                ("grant_type", "ig_exchange_token"),
                ("client_secret", credentials.AppSecret),
                ("access_token", shortToken));

            return ParseToken(longLived.RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Long-lived Instagram token exchange failed; using short-lived token.");
            return ParseToken(shortLived.RootElement);
        }
    }

    private static OAuthTokenResult ParseToken(JsonElement element)
    {
        var token = element.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Meta did not return an access token.");

        DateTime? expiresAt = null;
        if (element.TryGetProperty("expires_in", out var expiresIn) && expiresIn.TryGetInt64(out var seconds))
            expiresAt = DateTime.UtcNow.AddSeconds(seconds);

        return new OAuthTokenResult { AccessToken = token, ExpiresAt = expiresAt };
    }
}
