using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.DeveloperApps.Entities;

public class DeveloperAppSocialAccount : SocialAccountEntityBase
{
    public User? User { get; set; }
    public DeveloperAppPlatform? Platform { get; set; }
    public DeveloperAppSocialAuth? Auth { get; set; }
    public ICollection<DeveloperAppSocialProfile> Profiles { get; set; } = new List<DeveloperAppSocialProfile>();
    public ICollection<DeveloperAppSyncJob> SyncJobs { get; set; } = new List<DeveloperAppSyncJob>();
}
