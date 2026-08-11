namespace SocialMedia.Application.Settings;

/// <summary>
/// Root configuration for all Meta platforms (Facebook, Instagram, WhatsApp),
/// bound from the "MetaSettings" section of appsettings.
/// Each platform gets its own nested settings so redirect URIs, app credentials
/// and webhook verify tokens can differ per platform even though they all
/// talk to the same underlying Graph API.
/// </summary>
public class MetaSettings
{
    public const string SectionName = "MetaSettings";

    public FacebookSettings Facebook { get; set; } = new();
    public InstagramSettings Instagram { get; set; } = new();
    public InstagramLoginSettings InstagramLogin { get; set; } = new();
    public WhatsAppSettings WhatsApp { get; set; } = new();
}

/// <summary>
/// Base fields shared by every Meta platform's app configuration.
/// </summary>
public class MetaPlatformSettings
{
    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>
    /// OAuth redirect URI registered with Meta for this platform's login flow.
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// Graph API version to call, e.g. "v21.0".
    /// </summary>
    public string GraphApiVersion { get; set; } = "v21.0";

    /// <summary>
    /// Token Meta echoes back during webhook subscription verification.
    /// </summary>
    public string WebhookVerifyToken { get; set; } = string.Empty;
}

public class FacebookSettings : MetaPlatformSettings
{
}

/// <summary>
/// Instagram via Facebook Login. Prefer Facebook AppId/AppSecret for OAuth;
/// use this section for Instagram redirect URI and webhook verify token.
/// </summary>
public class InstagramSettings : MetaPlatformSettings
{
}

/// <summary>
/// Native Instagram Login (api.instagram.com / graph.instagram.com).
/// Uses an Instagram app App Id/Secret — not the Facebook Login path.
/// </summary>
public class InstagramLoginSettings : MetaPlatformSettings
{
}

/// <summary>
/// WhatsApp needs extra identifiers beyond the shared Meta app fields.
/// </summary>
public class WhatsAppSettings : MetaPlatformSettings
{
    /// <summary>
    /// Default Cloud API phone number id, used if a connected account doesn't specify its own.
    /// </summary>
    public string PhoneNumberId { get; set; } = string.Empty;

    /// <summary>
    /// Default WhatsApp Business Account id.
    /// </summary>
    public string WabaId { get; set; } = string.Empty;
}
