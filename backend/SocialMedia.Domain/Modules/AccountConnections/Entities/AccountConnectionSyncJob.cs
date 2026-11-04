using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountConnections.Entities;

public class AccountConnectionSyncJob : SyncJobEntityBase
{
    public AccountConnectionSocialAccount? SocialAccount { get; set; }
}
