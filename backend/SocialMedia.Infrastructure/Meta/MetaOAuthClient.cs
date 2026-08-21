using System.Text.Json;
using Microsoft.Extensions.Logging;
using SocialMedia.Application.Catalog;
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
        var codeNorm = MetaBaseUrlHelper.ResolveOAuthPlatform(platformCode, credentials.BaseUrl);
        return codeNorm switch
        {
            "instagram_login" => await ExchangeInstagramLoginCodeAsync(credentials, code, cancellationToken),
            _ => await ExchangeFacebookDialogCodeAsync(credentials, code, cancellationToken)
        };
    }

    public async Task<(string Id, string Name)> GetMeAsync(
        string platformCode,
        MetaOAuthCredentials credentials,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var codeNorm = MetaBaseUrlHelper.ResolveOAuthPlatform(platformCode, credentials.BaseUrl);
        if (codeNorm == "instagram_login")
        {
            using var doc = await _graph.GetInstagramAsync(
                credentials.GraphApiVersion, "me", accessToken, cancellationToken, ("fields", "user_id,username"));
            var id = doc.RootElement.TryGetProperty("user_id", out var uid)
                ? uid.GetString() ?? string.Empty
                : doc.RootElement.GetProperty("id").GetString() ?? string.Empty;
            var name = doc.RootElement.TryGetProperty("username", out var u)
                ? u.GetString() ?? "Instagram User"
                : "Instagram User";
            return (id, name);
        }

        var graphHost = ResolveGraphHost(credentials);
        using var fbDoc = await _graph.GetOnHostAsync(
            graphHost, credentials.GraphApiVersion, "me", accessToken, cancellationToken, ("fields", "id,name"));
        var fbId = fbDoc.RootElement.GetProperty("id").GetString() ?? string.Empty;
        var fbName = fbDoc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? "Meta User" : "Meta User";
        return (fbId, fbName);
    }

    public async Task<IReadOnlyList<MetaPageInfo>> ListPagesAsync(
        string platformCode,
        MetaOAuthCredentials credentials,
        string userAccessToken,
        CancellationToken cancellationToken = default)
    {
        var version = NormalizeGraphVersion(credentials.GraphApiVersion);
        var pages = await _graph.ListPagesAsync(version, userAccessToken, cancellationToken);
        if (pages.Count > 0)
            return pages;

        var pageIds = await TryGetGranularPageIdsAsync(credentials, userAccessToken, cancellationToken);
        if (pageIds.Count == 0)
            return pages;

        var fallback = new List<MetaPageInfo>();
        foreach (var pageId in pageIds)
        {
            try
            {
                var info = await _graph.GetPageByIdAsync(version, pageId, userAccessToken, cancellationToken);
                if (!string.IsNullOrWhiteSpace(info.PageId))
                    fallback.Add(info);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load Meta page {PageId} from granular OAuth scopes.", pageId);
            }
        }

        return fallback;
    }

    public async Task<string?> DescribeTokenScopesAsync(
        MetaOAuthCredentials credentials,
        string userAccessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var version = NormalizeGraphVersion(credentials.GraphApiVersion);
            var graphHost = ResolveGraphHost(credentials);
            var appToken = $"{credentials.AppId}|{credentials.AppSecret}";
            using var doc = await _graph.GetOnHostAsync(
                graphHost,
                version,
                "debug_token",
                appToken,
                cancellationToken,
                ("input_token", userAccessToken));

            if (!doc.RootElement.TryGetProperty("data", out var data))
                return null;

            if (data.TryGetProperty("scopes", out var scopes) && scopes.ValueKind == JsonValueKind.Array)
            {
                var list = scopes.EnumerateArray()
                    .Select(s => s.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
                if (list.Count > 0)
                    return string.Join(", ", list!);
            }

            if (data.TryGetProperty("granular_scopes", out var granular) && granular.ValueKind == JsonValueKind.Array)
            {
                var parts = granular.EnumerateArray()
                    .Select(item =>
                    {
                        var scope = item.TryGetProperty("scope", out var s) ? s.GetString() : null;
                        if (string.IsNullOrWhiteSpace(scope))
                            return null;
                        if (item.TryGetProperty("target_ids", out var ids) && ids.ValueKind == JsonValueKind.Array)
                        {
                            var idList = ids.EnumerateArray()
                                .Select(id => id.GetString())
                                .Where(id => !string.IsNullOrWhiteSpace(id))
                                .ToList();
                            if (idList.Count > 0)
                                return $"{scope} ({string.Join(", ", idList!)})";
                        }
                        return scope;
                    })
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();
                if (parts.Count > 0)
                    return string.Join("; ", parts!);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not inspect Meta token scopes.");
            return null;
        }
    }

    private async Task<IReadOnlySet<string>> TryGetGranularPageIdsAsync(
        MetaOAuthCredentials credentials,
        string userAccessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var version = NormalizeGraphVersion(credentials.GraphApiVersion);
            var graphHost = ResolveGraphHost(credentials);
            var appToken = $"{credentials.AppId}|{credentials.AppSecret}";
            using var doc = await _graph.GetOnHostAsync(
                graphHost,
                version,
                "debug_token",
                appToken,
                cancellationToken,
                ("input_token", userAccessToken));

            if (!doc.RootElement.TryGetProperty("data", out var data))
                return new HashSet<string>(StringComparer.Ordinal);

            if (!data.TryGetProperty("granular_scopes", out var granular) || granular.ValueKind != JsonValueKind.Array)
                return new HashSet<string>(StringComparer.Ordinal);

            var pageIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in granular.EnumerateArray())
            {
                var scope = item.TryGetProperty("scope", out var s) ? s.GetString() : null;
                if (string.IsNullOrWhiteSpace(scope) || !IsPageRelatedScope(scope))
                    continue;

                if (!item.TryGetProperty("target_ids", out var ids) || ids.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var id in ids.EnumerateArray())
                {
                    var pageId = id.GetString();
                    if (!string.IsNullOrWhiteSpace(pageId))
                        pageIds.Add(pageId);
                }
            }

            return pageIds;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read granular page ids from Meta token.");
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private static bool IsPageRelatedScope(string scope)
    {
        var value = scope.Trim().ToLowerInvariant();
        return value.StartsWith("pages_", StringComparison.Ordinal)
            || value.StartsWith("instagram_", StringComparison.Ordinal)
            || value is "business_management";
    }

    private static string ResolveGraphHost(MetaOAuthCredentials credentials) =>
        MetaBaseUrlHelper.Resolve(credentials.BaseUrl, "facebook");

    private static string NormalizeGraphVersion(string? graphVersion)
    {
        var value = (graphVersion ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(value) ? "v21.0" : value;
    }

    private async Task<OAuthTokenResult> ExchangeFacebookDialogCodeAsync(
        MetaOAuthCredentials credentials,
        string code,
        CancellationToken cancellationToken)
    {
        var graphHost = ResolveGraphHost(credentials);
        using var shortLived = await _graph.GetOnHostAsync(
            graphHost, credentials.GraphApiVersion, "oauth/access_token", string.Empty, cancellationToken,
            ("client_id", credentials.AppId),
            ("client_secret", credentials.AppSecret),
            ("redirect_uri", credentials.RedirectUri),
            ("code", code));

        var shortToken = shortLived.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Meta did not return an access token.");

        try
        {
            using var longLived = await _graph.GetOnHostAsync(
                graphHost, credentials.GraphApiVersion, "oauth/access_token", string.Empty, cancellationToken,
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
