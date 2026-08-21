using SocialMedia.Domain.Common;

namespace SocialMedia.Domain.Entities;

/// <summary>
/// A user-owned Meta developer app configuration with its own App Id, secret, and callback URL.
/// Used by App Connections so multiple Meta apps can coexist in one workspace.
/// </summary>
public class MetaAppConnection : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Friendly label shown in the App Connections UI.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>facebook, instagram, instagram_login, or whatsapp.</summary>
    public string PlatformCode { get; set; } = string.Empty;

    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public string GraphApiVersion { get; set; } = "v21.0";

    /// <summary>Comma-separated Meta OAuth scopes for this app connection.</summary>
    public string Scopes { get; set; } = string.Empty;

    public User? User { get; set; }
    public ICollection<SocialAccount> SocialAccounts { get; set; } = new List<SocialAccount>();
}
