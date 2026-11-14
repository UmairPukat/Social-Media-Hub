using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountManagement.Entities;

public class AccountManagementSocialAuth : SocialAuthEntityBase
{
    public AccountManagementSocialAccount? SocialAccount { get; set; }
}
