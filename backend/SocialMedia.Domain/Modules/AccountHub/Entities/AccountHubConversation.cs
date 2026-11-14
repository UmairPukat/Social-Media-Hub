using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountHub.Entities;

public class AccountHubConversation : ConversationEntityBase
{
    public AccountHubSocialProfile? SocialProfile { get; set; }
    public ICollection<AccountHubMessage> Messages { get; set; } = new List<AccountHubMessage>();
}
