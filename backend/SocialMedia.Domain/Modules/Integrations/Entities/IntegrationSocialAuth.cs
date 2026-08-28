using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.Integrations.Entities;

public class IntegrationSocialAuth : SocialAuthEntityBase
{
    public IntegrationSocialAccount? SocialAccount { get; set; }
}
