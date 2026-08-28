using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.Integrations.Entities;

public class IntegrationPlatform : PlatformEntityBase
{
    public ICollection<IntegrationSocialAccount> SocialAccounts { get; set; } = new List<IntegrationSocialAccount>();
}
