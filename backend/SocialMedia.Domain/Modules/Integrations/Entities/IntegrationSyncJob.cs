using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.Integrations.Entities;

public class IntegrationSyncJob : SyncJobEntityBase
{
    public IntegrationSocialAccount? SocialAccount { get; set; }
}
