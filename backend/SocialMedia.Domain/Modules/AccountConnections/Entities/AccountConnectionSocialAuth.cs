using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountConnections.Entities;

public class AccountConnectionSocialAuth : SocialAuthEntityBase
{
    public AccountConnectionSocialAccount? SocialAccount { get; set; }
}
