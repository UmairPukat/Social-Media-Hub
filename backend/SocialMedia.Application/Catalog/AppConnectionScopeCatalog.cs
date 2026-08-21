namespace SocialMedia.Application.Catalog;

/// <summary>
/// OAuth scopes used by App Connections — intentionally separate from Integrations defaults
/// so each Meta developer app can request only the permissions it needs.
/// </summary>
public static class AppConnectionScopeCatalog
{
    private static readonly Dictionary<string, string> Defaults = new(StringComparer.OrdinalIgnoreCase)
    {
        // Content, inbox, and page management — no ads scopes.
        ["facebook"] =
            "public_profile,email,pages_show_list,pages_read_engagement,pages_read_user_content," +
            "pages_manage_posts,pages_manage_engagement,pages_manage_metadata,pages_messaging,business_management",

        ["instagram"] =
            "public_profile,email,pages_show_list,pages_read_engagement,pages_read_user_content," +
            "pages_manage_metadata,pages_messaging,business_management,instagram_basic,instagram_manage_comments," +
            "instagram_manage_messages,instagram_content_publish",

        ["instagram_login"] =
            "instagram_business_basic,instagram_business_content_publish,instagram_business_manage_messages," +
            "instagram_business_manage_comments",

        ["whatsapp"] =
            "whatsapp_business_management,whatsapp_business_messaging,business_management"
    };

    public static string GetDefault(string platformCode)
    {
        var code = (platformCode ?? string.Empty).Trim().ToLowerInvariant();
        return Defaults.TryGetValue(code, out var scopes) ? scopes : string.Empty;
    }

    public static IReadOnlyDictionary<string, string> All => Defaults;
}
