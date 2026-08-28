using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.DeveloperApps.Entities;

public class DeveloperAppMessageAttachment : MessageAttachmentEntityBase
{
    public DeveloperAppMessage? Message { get; set; }
}
