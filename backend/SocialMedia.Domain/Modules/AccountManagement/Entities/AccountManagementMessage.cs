using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountManagement.Entities;

public class AccountManagementMessage : MessageEntityBase
{
    public AccountManagementConversation? Conversation { get; set; }
    public ICollection<AccountManagementMessageAttachment> Attachments { get; set; } = new List<AccountManagementMessageAttachment>();
}
