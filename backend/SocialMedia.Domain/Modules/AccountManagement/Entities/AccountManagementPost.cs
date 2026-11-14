using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountManagement.Entities;

public class AccountManagementPost : PostEntityBase
{
    public AccountManagementSocialProfile? SocialProfile { get; set; }
    public AccountManagementPlatform? Platform { get; set; }
    public ICollection<AccountManagementMedia> MediaItems { get; set; } = new List<AccountManagementMedia>();
    public ICollection<AccountManagementComment> Comments { get; set; } = new List<AccountManagementComment>();
}
