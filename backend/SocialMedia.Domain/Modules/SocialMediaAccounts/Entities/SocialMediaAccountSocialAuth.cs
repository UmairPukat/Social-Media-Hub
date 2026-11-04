using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.SocialMediaAccounts.Entities;

public class SocialMediaAccountSocialAuth : SocialAuthEntityBase
{
    public SocialMediaAccountSocialAccount? SocialAccount { get; set; }
}
