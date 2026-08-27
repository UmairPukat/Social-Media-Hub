namespace SocialMedia.Application.Catalog;

/// <summary>
/// Identifies which UI menu owns a platform catalog row or connected account.
/// </summary>
public static class MenuTypes
{
    public const string Integration = "integration";
    public const string AppConnection = "app_connection";
    public const string DeveloperApp = "developer_app";

    public static string Normalize(string? menuType)
    {
        var value = (menuType ?? Integration).Trim().ToLowerInvariant();
        return value switch
        {
            Integration => Integration,
            AppConnection => AppConnection,
            DeveloperApp => DeveloperApp,
            _ => Integration
        };
    }

    public static bool IsKnown(string? menuType)
    {
        var value = (menuType ?? string.Empty).Trim().ToLowerInvariant();
        return value is Integration or AppConnection or DeveloperApp;
    }
}
