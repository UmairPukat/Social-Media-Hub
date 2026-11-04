using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.SocialMediaAccounts.Entities;

public class SocialMediaAccountMessageAttachment : MessageAttachmentEntityBase
{
    public SocialMediaAccountMessage? Message { get; set; }
}
