using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountManagement.Entities;

public class AccountManagementPlatform : PlatformEntityBase
{
    public ICollection<AccountManagementSocialAccount> SocialAccounts { get; set; } = new List<AccountManagementSocialAccount>();
}
