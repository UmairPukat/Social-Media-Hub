using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AppConnections.Entities;

public class AppConnectionSocialAccount : SocialAccountEntityBase
{
    public User? User { get; set; }
    public AppConnectionPlatform? Platform { get; set; }
    public AppConnectionSocialAuth? Auth { get; set; }
    public ICollection<AppConnectionSocialProfile> Profiles { get; set; } = new List<AppConnectionSocialProfile>();
    public ICollection<AppConnectionSyncJob> SyncJobs { get; set; } = new List<AppConnectionSyncJob>();
}
