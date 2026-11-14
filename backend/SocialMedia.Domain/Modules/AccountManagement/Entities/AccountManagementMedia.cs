using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountManagement.Entities;

public class AccountManagementMedia : MediaEntityBase
{
    public AccountManagementPost? Post { get; set; }
}
