using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountManagement.Entities;

public class AccountManagementMessageAttachment : MessageAttachmentEntityBase
{
    public AccountManagementMessage? Message { get; set; }
}
