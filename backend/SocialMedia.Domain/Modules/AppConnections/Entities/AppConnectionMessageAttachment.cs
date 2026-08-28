using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AppConnections.Entities;

public class AppConnectionMessageAttachment : MessageAttachmentEntityBase
{
    public AppConnectionMessage? Message { get; set; }
}
