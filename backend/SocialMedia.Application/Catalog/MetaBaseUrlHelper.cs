namespace SocialMedia.Application.Catalog;

/// <summary>
/// Normalizes Meta Graph / OAuth base URLs for App Connections.
/// </summary>
public static class MetaBaseUrlHelper
{
    public const string FacebookGraph = "https://graph.facebook.com";
    public const string InstagramGraph = "https://graph.instagram.com";

    public static string DefaultForPlatform(string platformCode) =>
        (platformCode ?? string.Empty).Trim().Equals("instagram_login", StringComparison.OrdinalIgnoreCase)
            ? InstagramGraph
            : FacebookGraph;

    public static string Normalize(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return FacebookGraph;

        var trimmed = baseUrl.Trim().TrimEnd('/');
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "https://" + trimmed;
        }

        return trimmed;
    }

    public static string Resolve(string? baseUrl, string platformCode) =>
        string.IsNullOrWhiteSpace(baseUrl)
            ? DefaultForPlatform(platformCode)
            : Normalize(baseUrl);

    public static bool UsesInstagramStack(string? baseUrl) =>
        Normalize(baseUrl).Contains("instagram", StringComparison.OrdinalIgnoreCase);

    public static string ResolveOAuthPlatform(string platformCode, string? baseUrl) =>
        UsesInstagramStack(baseUrl) || platformCode.Equals("instagram_login", StringComparison.OrdinalIgnoreCase)
            ? "instagram_login"
            : platformCode;

    public static string BuildAuthorizeUrl(
        string platformCode,
        string? baseUrl,
        string appId,
        string callbackUrl,
        string graphApiVersion,
        string state,
        string scopes)
    {
        var useInstagram = ResolveOAuthPlatform(platformCode, baseUrl) == "instagram_login";

        if (useInstagram)
        {
            return "https://www.instagram.com/oauth/authorize"
                   + $"?client_id={Uri.EscapeDataString(appId)}"
                   + $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}"
                   + $"&state={Uri.EscapeDataString(state)}"
                   + $"&scope={Uri.EscapeDataString(scopes)}"
                   + "&response_type=code";
        }

        var version = string.IsNullOrWhiteSpace(graphApiVersion) ? "v21.0" : graphApiVersion.Trim().Trim('/');
        return $"https://www.facebook.com/{version}/dialog/oauth"
               + $"?client_id={Uri.EscapeDataString(appId)}"
               + $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}"
               + $"&state={Uri.EscapeDataString(state)}"
               + $"&scope={Uri.EscapeDataString(scopes)}"
               + "&response_type=code";
    }
}
