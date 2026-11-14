using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountManagement.Entities;

public class AccountManagementConversation : ConversationEntityBase
{
    public AccountManagementSocialProfile? SocialProfile { get; set; }
    public ICollection<AccountManagementMessage> Messages { get; set; } = new List<AccountManagementMessage>();
}
