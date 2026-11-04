using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountConnections.Entities;

public class AccountConnectionSocialAccount : SocialAccountEntityBase
{
    public User? User { get; set; }
    public AccountConnectionPlatform? Platform { get; set; }
    public AccountConnectionSocialAuth? Auth { get; set; }
    public ICollection<AccountConnectionSocialProfile> Profiles { get; set; } = new List<AccountConnectionSocialProfile>();
    public ICollection<AccountConnectionSyncJob> SyncJobs { get; set; } = new List<AccountConnectionSyncJob>();
}
