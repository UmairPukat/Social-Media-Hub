using SocialMedia.Domain.Common;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Domain.Modules.Common.Entities;

public abstract class SocialAccountEntityBase : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid PlatformId { get; set; }
    public string ExternalAccountId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? ProfileImage { get; set; }
    public SocialAccountStatus Status { get; set; } = SocialAccountStatus.Disconnected;
    public DateTime? ConnectedAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? MetadataJson { get; set; }
}
