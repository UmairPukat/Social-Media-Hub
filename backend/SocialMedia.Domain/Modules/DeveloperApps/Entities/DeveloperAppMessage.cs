using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.DeveloperApps.Entities;

public class DeveloperAppMessage : MessageEntityBase
{
    public DeveloperAppConversation? Conversation { get; set; }
    public ICollection<DeveloperAppMessageAttachment> Attachments { get; set; } = new List<DeveloperAppMessageAttachment>();
}
