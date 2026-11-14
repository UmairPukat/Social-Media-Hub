using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountHub.Entities;

public class AccountHubPlatform : PlatformEntityBase
{
    public ICollection<AccountHubSocialAccount> SocialAccounts { get; set; } = new List<AccountHubSocialAccount>();
}
