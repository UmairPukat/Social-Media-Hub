using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.DeveloperApps.Entities;

public class DeveloperAppSyncJob : SyncJobEntityBase
{
    public DeveloperAppSocialAccount? SocialAccount { get; set; }
}
