using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountHub.Entities;

public class AccountHubSocialAccount : SocialAccountEntityBase
{
    public User? User { get; set; }
    public AccountHubPlatform? Platform { get; set; }
    public AccountHubSocialAuth? Auth { get; set; }
    public ICollection<AccountHubSocialProfile> Profiles { get; set; } = new List<AccountHubSocialProfile>();
    public ICollection<AccountHubSyncJob> SyncJobs { get; set; } = new List<AccountHubSyncJob>();
}
