using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountHub.Entities;

public class AccountHubSocialAuth : SocialAuthEntityBase
{
    public AccountHubSocialAccount? SocialAccount { get; set; }
}
