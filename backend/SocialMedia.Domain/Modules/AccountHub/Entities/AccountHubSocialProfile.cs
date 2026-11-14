using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountHub.Entities;

public class AccountHubSocialProfile : SocialProfileEntityBase
{
    public AccountHubSocialAccount? SocialAccount { get; set; }
    public ICollection<AccountHubPost> Posts { get; set; } = new List<AccountHubPost>();
    public ICollection<AccountHubConversation> Conversations { get; set; } = new List<AccountHubConversation>();
}
