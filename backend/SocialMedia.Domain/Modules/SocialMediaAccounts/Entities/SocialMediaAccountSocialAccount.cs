using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.SocialMediaAccounts.Entities;

public class SocialMediaAccountSocialAccount : SocialAccountEntityBase
{
    public User? User { get; set; }
    public SocialMediaAccountPlatform? Platform { get; set; }
    public SocialMediaAccountSocialAuth? Auth { get; set; }
    public ICollection<SocialMediaAccountSocialProfile> Profiles { get; set; } = new List<SocialMediaAccountSocialProfile>();
    public ICollection<SocialMediaAccountSyncJob> SyncJobs { get; set; } = new List<SocialMediaAccountSyncJob>();
}
