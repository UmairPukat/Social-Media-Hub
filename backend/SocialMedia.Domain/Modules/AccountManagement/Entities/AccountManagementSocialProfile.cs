using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountManagement.Entities;

public class AccountManagementSocialProfile : SocialProfileEntityBase
{
    public AccountManagementSocialAccount? SocialAccount { get; set; }
    public ICollection<AccountManagementPost> Posts { get; set; } = new List<AccountManagementPost>();
    public ICollection<AccountManagementConversation> Conversations { get; set; } = new List<AccountManagementConversation>();
}
