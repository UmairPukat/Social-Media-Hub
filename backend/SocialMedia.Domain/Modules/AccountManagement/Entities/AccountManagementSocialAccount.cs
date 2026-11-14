using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountManagement.Entities;

public class AccountManagementSocialAccount : SocialAccountEntityBase
{
    public User? User { get; set; }
    public AccountManagementPlatform? Platform { get; set; }
    public AccountManagementSocialAuth? Auth { get; set; }
    public ICollection<AccountManagementSocialProfile> Profiles { get; set; } = new List<AccountManagementSocialProfile>();
    public ICollection<AccountManagementSyncJob> SyncJobs { get; set; } = new List<AccountManagementSyncJob>();
}
