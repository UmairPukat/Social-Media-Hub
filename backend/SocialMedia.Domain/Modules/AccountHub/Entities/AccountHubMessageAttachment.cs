using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountHub.Entities;

public class AccountHubMessageAttachment : MessageAttachmentEntityBase
{
    public AccountHubMessage? Message { get; set; }
}
