using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountConnections.Entities;

public class AccountConnectionPlatform : PlatformEntityBase
{
    public ICollection<AccountConnectionSocialAccount> SocialAccounts { get; set; } = new List<AccountConnectionSocialAccount>();
}
