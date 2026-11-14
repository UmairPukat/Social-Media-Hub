using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountManagement.Entities;

public class AccountManagementSyncJob : SyncJobEntityBase
{
    public AccountManagementSocialAccount? SocialAccount { get; set; }
}
