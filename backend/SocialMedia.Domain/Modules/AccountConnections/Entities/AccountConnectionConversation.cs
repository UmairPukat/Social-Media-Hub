using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountConnections.Entities;

public class AccountConnectionConversation : ConversationEntityBase
{
    public AccountConnectionSocialProfile? SocialProfile { get; set; }
    public ICollection<AccountConnectionMessage> Messages { get; set; } = new List<AccountConnectionMessage>();
}
