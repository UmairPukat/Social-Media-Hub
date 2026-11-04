using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountConnections.Entities;

public class AccountConnectionSocialProfile : SocialProfileEntityBase
{
    public AccountConnectionSocialAccount? SocialAccount { get; set; }
    public ICollection<AccountConnectionPost> Posts { get; set; } = new List<AccountConnectionPost>();
    public ICollection<AccountConnectionConversation> Conversations { get; set; } = new List<AccountConnectionConversation>();
}
