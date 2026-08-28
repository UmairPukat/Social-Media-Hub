using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AppConnections.Entities;

public class AppConnectionSyncJob : SyncJobEntityBase
{
    public AppConnectionSocialAccount? SocialAccount { get; set; }
}
