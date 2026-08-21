using SocialMedia.Domain.Common;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Domain.Entities;

/// <summary>
/// One connected social account for an application user.
/// </summary>
public class SocialAccount : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid PlatformId { get; set; }

    /// <summary>Set when connected via App Connections; null for the default Integrations flow.</summary>
    public Guid? MetaAppConnectionId { get; set; }

    /// <summary>External user / business id from the platform.</summary>
    public string ExternalAccountId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? ProfileImage { get; set; }

    public SocialAccountStatus Status { get; set; } = SocialAccountStatus.Disconnected;
    public DateTime? ConnectedAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? MetadataJson { get; set; }

    public User? User { get; set; }
    public Platform? Platform { get; set; }
    public MetaAppConnection? MetaAppConnection { get; set; }
    public SocialAuth? Auth { get; set; }
    public ICollection<SocialProfile> Profiles { get; set; } = new List<SocialProfile>();
    public ICollection<SyncJob> SyncJobs { get; set; } = new List<SyncJob>();
}
