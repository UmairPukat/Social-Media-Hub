using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountConnections.Entities;

public class AccountConnectionMessageAttachment : MessageAttachmentEntityBase
{
    public AccountConnectionMessage? Message { get; set; }
}
