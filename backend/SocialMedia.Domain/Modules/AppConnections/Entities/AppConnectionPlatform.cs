using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AppConnections.Entities;

public class AppConnectionPlatform : PlatformEntityBase
{
    public ICollection<AppConnectionSocialAccount> SocialAccounts { get; set; } = new List<AppConnectionSocialAccount>();
}
