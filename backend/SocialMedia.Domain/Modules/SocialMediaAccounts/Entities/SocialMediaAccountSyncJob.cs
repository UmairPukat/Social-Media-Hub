using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.SocialMediaAccounts.Entities;

public class SocialMediaAccountSyncJob : SyncJobEntityBase
{
    public SocialMediaAccountSocialAccount? SocialAccount { get; set; }
}
