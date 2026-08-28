using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Application.Meta;

/// <summary>
/// Resolves Instagram connection type from existing platform / profile fields.
/// No extra database column is required: Platform.Code and ProfileType already distinguish the two paths.
/// </summary>
public static class InstagramConnectionResolver
{
    public const string FacebookLoginPlatformCode = "instagram";
    public const string InstagramLoginPlatformCode = "instagram_login";

    /// <summary>Inbox UI always shows a single Instagram tab — never expose connection type to the client.</summary>
    public static string ToInboxPlatformCode(string? platformCode)
        => IsInstagramPlatform(platformCode) ? FacebookLoginPlatformCode : (platformCode ?? string.Empty);

    public static bool IsInstagramPlatform(string? platformCode)
        => string.Equals(platformCode, FacebookLoginPlatformCode, StringComparison.OrdinalIgnoreCase)
           || string.Equals(platformCode, InstagramLoginPlatformCode, StringComparison.OrdinalIgnoreCase);

    public static InstagramConnectionType FromPlatformCode(string? platformCode)
        => string.Equals(platformCode, InstagramLoginPlatformCode, StringComparison.OrdinalIgnoreCase)
            ? InstagramConnectionType.InstagramLogin
            : InstagramConnectionType.FacebookLogin;

    public static InstagramConnectionType FromProfile(SocialProfileEntityBase profile, string? platformCode = null)
    {
        if (profile.ProfileType == ProfileType.InstagramLogin)
            return InstagramConnectionType.InstagramLogin;

        if (profile.ProfileType == ProfileType.InstagramBusiness)
            return InstagramConnectionType.FacebookLogin;

        return FromPlatformCode(platformCode);
    }

    public static string ToLogLabel(InstagramConnectionType connectionType)
        => connectionType == InstagramConnectionType.InstagramLogin ? "InstagramLogin" : "FacebookLogin";
}
