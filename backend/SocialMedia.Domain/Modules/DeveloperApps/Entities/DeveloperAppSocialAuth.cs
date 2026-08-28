using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.DeveloperApps.Entities;

public class DeveloperAppSocialAuth : SocialAuthEntityBase
{
    public DeveloperAppSocialAccount? SocialAccount { get; set; }
}
