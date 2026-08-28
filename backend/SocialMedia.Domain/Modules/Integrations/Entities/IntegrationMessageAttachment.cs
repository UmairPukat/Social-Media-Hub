using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.Integrations.Entities;

public class IntegrationMessageAttachment : MessageAttachmentEntityBase
{
    public IntegrationMessage? Message { get; set; }
}
