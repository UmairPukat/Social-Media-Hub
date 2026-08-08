using SocialMedia.Domain.Enums;

namespace SocialMedia.Application.DTOs.Integration;

public class PlatformCardDto
{
    public Guid PlatformId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string Category { get; set; } = string.Empty;
    public string CategoryLabel { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool CanConnect { get; set; }
    public bool IsConnected { get; set; }
    public string? AccountName { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public bool SupportsComments { get; set; }
    public bool SupportsMessages { get; set; }
    public bool SupportsPosts { get; set; }
}

/// <summary>
/// Meta popup redirect payload — frontend sends the authorization code here.
/// </summary>
public class OAuthCallbackRequest
{
    /// <summary>Which Meta product the code belongs to: facebook, instagram, or whatsapp.</summary>
    public string PlatformCode { get; set; } = string.Empty;

    /// <summary>Authorization code returned by Meta in the redirect query string.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Must match the redirect_uri used when opening the Meta popup.
    /// If empty, the configured RedirectUri from appsettings is used.
    /// </summary>
    public string? RedirectUri { get; set; }
}

/// <summary>Starts Meta Login — returns the dialog URL that uses the backend Callback redirect.</summary>
public class BeginOAuthRequest
{
    public string PlatformCode { get; set; } = string.Empty;
}

public class BeginOAuthResponse
{
    public string AuthUrl { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string PlatformCode { get; set; } = string.Empty;
}

/// <summary>Result of Meta's browser redirect to the shared backend Callback URL.</summary>
public class MetaRedirectResult
{
    public bool Ok { get; set; }
    public string PlatformCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<string> FrontendOrigins { get; set; } = Array.Empty<string>();
}

public class SocialAccountDto
{
    public Guid Id { get; set; }
    public Guid PlatformId { get; set; }
    public string PlatformCode { get; set; } = string.Empty;
    public string PlatformName { get; set; } = string.Empty;
    public string ExternalAccountId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Username { get; set; }
    public SocialAccountStatus Status { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public IReadOnlyList<SocialProfileDto> Profiles { get; set; } = Array.Empty<SocialProfileDto>();

    /// <summary>
    /// True when the token is stored but no page has been chosen yet — the frontend
    /// then shows the page picker before any syncing happens.
    /// </summary>
    public bool RequiresPageSelection { get; set; }
}

/// <summary>
/// One selectable Facebook Page. Access tokens are never included.
/// </summary>
public class MetaPageDto
{
    public string PageId { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;
    public string? PageImage { get; set; }
    public string? InstagramId { get; set; }
    public string? InstagramUsername { get; set; }

    /// <summary>False when the page cannot be used for this platform (e.g. no linked Instagram account).</summary>
    public bool IsEligible { get; set; }

    /// <summary>Reason shown in the picker when <see cref="IsEligible"/> is false.</summary>
    public string? IneligibleReason { get; set; }

    /// <summary>True when this page is the one already connected for the platform.</summary>
    public bool IsSelected { get; set; }
}

/// <summary>Frontend sends the single page the user ticked in the picker.</summary>
public class SelectPageRequest
{
    public string PlatformCode { get; set; } = string.Empty;
    public string PageId { get; set; } = string.Empty;
}

/// <summary>
/// Everything shown in the connected-account details popup, including a live read of the
/// page's webhook subscription so a broken setup is visible without opening Meta.
/// </summary>
public class ConnectionDetailsDto
{
    public string PlatformCode { get; set; } = string.Empty;
    public string PlatformName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public SocialAccountStatus Status { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public DateTime? LastSyncAt { get; set; }

    public string? PageId { get; set; }
    public string? PageName { get; set; }
    public string? PageImage { get; set; }
    public string? InstagramId { get; set; }
    public string? InstagramUsername { get; set; }

    /// <summary>
    /// Page access token stored after Meta connect (Facebook Page token for both Facebook and Instagram).
    /// Empty when the account is connected but the token was cleared.
    /// </summary>
    public string? AccessToken { get; set; }

    public bool WebhookSubscribed { get; set; }
    public IReadOnlyList<string> SubscribedFields { get; set; } = Array.Empty<string>();

    /// <summary>Why the live subscription check failed, when it did.</summary>
    public string? WebhookError { get; set; }

    public IReadOnlyList<SocialProfileDto> Profiles { get; set; } = Array.Empty<SocialProfileDto>();
}

public class SocialProfileDto
{
    public Guid Id { get; set; }
    public string ExternalProfileId { get; set; } = string.Empty;
    public string ProfileType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Username { get; set; }
}
