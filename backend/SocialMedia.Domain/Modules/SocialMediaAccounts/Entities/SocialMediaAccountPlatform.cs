using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.SocialMediaAccounts.Entities;

public class SocialMediaAccountPlatform : PlatformEntityBase
{
    public ICollection<SocialMediaAccountSocialAccount> SocialAccounts { get; set; } = new List<SocialMediaAccountSocialAccount>();
}
