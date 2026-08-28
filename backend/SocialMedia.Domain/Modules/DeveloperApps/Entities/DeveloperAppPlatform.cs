using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.DeveloperApps.Entities;

public class DeveloperAppPlatform : PlatformEntityBase
{
    public ICollection<DeveloperAppSocialAccount> SocialAccounts { get; set; } = new List<DeveloperAppSocialAccount>();
}
