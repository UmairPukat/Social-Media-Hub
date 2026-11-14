using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountHub.Entities;

public class AccountHubSyncJob : SyncJobEntityBase
{
    public AccountHubSocialAccount? SocialAccount { get; set; }
}
