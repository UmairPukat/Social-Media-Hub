using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountHub.Entities;

public class AccountHubMessage : MessageEntityBase
{
    public AccountHubConversation? Conversation { get; set; }
    public ICollection<AccountHubMessageAttachment> Attachments { get; set; } = new List<AccountHubMessageAttachment>();
}
