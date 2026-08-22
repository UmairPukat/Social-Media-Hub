namespace SocialMedia.Application.Catalog;

public static class InstagramLoginScopeHelper
{
    private static readonly string[] InvalidScopePrefixes = ["pages_", "public_profile", "business_management", "email"];

    public static string Sanitize(string scopes)
    {
        var allowed = scopes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.StartsWith("instagram_business_", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!allowed.Any(s => s.Equals("instagram_business_basic", StringComparison.OrdinalIgnoreCase)))
            allowed.Insert(0, "instagram_business_basic");

        return string.Join(",", allowed);
    }

    public static string FormatForAuthorizeUrl(string sanitizedCommaScopes) =>
        string.Join(',',
            sanitizedCommaScopes
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public static string? DetectInvalidStoredScopes(string? rawScopes)
    {
        if (string.IsNullOrWhiteSpace(rawScopes))
            return null;

        var invalid = rawScopes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => IsInvalidForInstagramLogin(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (invalid.Count == 0)
            return null;

        return "Instagram Login cannot use Facebook scopes: "
               + string.Join(", ", invalid)
               + ". Remove them and keep only instagram_business_* (click Reset defaults, save, reconnect).";
    }

    public static bool ContainsInstagramBusinessScopes(string? scopes) =>
        !string.IsNullOrWhiteSpace(scopes)
        && scopes.Contains("instagram_business_", StringComparison.OrdinalIgnoreCase);

    private static bool IsInvalidForInstagramLogin(string scope)
    {
        var value = scope.Trim();
        if (value.StartsWith("instagram_business_", StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (var prefix in InvalidScopePrefixes)
        {
            if (prefix.EndsWith('_'))
            {
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (value.Equals(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return value.StartsWith("instagram_", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("pages_", StringComparison.OrdinalIgnoreCase);
    }
}
