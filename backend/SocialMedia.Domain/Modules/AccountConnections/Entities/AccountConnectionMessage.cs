using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountConnections.Entities;

public class AccountConnectionMessage : MessageEntityBase
{
    public AccountConnectionConversation? Conversation { get; set; }
    public ICollection<AccountConnectionMessageAttachment> Attachments { get; set; } = new List<AccountConnectionMessageAttachment>();
}
