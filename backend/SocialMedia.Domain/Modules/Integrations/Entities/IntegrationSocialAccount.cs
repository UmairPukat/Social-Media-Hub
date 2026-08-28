using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.Integrations.Entities;

public class IntegrationSocialAccount : SocialAccountEntityBase
{
    public User? User { get; set; }
    public IntegrationPlatform? Platform { get; set; }
    public IntegrationSocialAuth? Auth { get; set; }
    public ICollection<IntegrationSocialProfile> Profiles { get; set; } = new List<IntegrationSocialProfile>();
    public ICollection<IntegrationSyncJob> SyncJobs { get; set; } = new List<IntegrationSyncJob>();
}
