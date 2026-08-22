using System.Text.RegularExpressions;

namespace SocialMedia.Application.Catalog;

public static partial class MetaAppIdValidator
{
    [GeneratedRegex(@"^\d{8,20}$")]
    private static partial Regex NumericAppIdPattern();

    public static bool IsValid(string? appId) =>
        !string.IsNullOrWhiteSpace(appId) && NumericAppIdPattern().IsMatch(appId.Trim());

    public static string? ValidateOrError(string? appId, string fieldLabel = "App Id")
    {
        if (string.IsNullOrWhiteSpace(appId))
            return $"{fieldLabel} is required.";

        var trimmed = appId.Trim();
        if (IsValid(trimmed))
            return null;

        return $"{fieldLabel} must be a numeric Meta app id (digits only, typically 15–16 characters). "
               + $"The value \"{trimmed}\" is not valid — copy the id from the Meta Developer Dashboard, not an email or username.";
    }
}
