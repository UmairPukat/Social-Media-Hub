using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AppConnections.Entities;

public class AppConnectionSocialAuth : SocialAuthEntityBase
{
    public AppConnectionSocialAccount? SocialAccount { get; set; }
}
