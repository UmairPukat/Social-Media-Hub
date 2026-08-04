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
    /// <summary>Authorization code returned by Meta in the redirect query string.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Must match the redirect_uri used when opening the Meta popup.
    /// If empty, the configured RedirectUri from appsettings is used.
    /// </summary>
    public string? RedirectUri { get; set; }
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
}

public class SocialProfileDto
{
    public Guid Id { get; set; }
    public string ExternalProfileId { get; set; } = string.Empty;
    public string ProfileType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Username { get; set; }
}
