using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.Integrations.Entities;

public class IntegrationMessage : MessageEntityBase
{
    public IntegrationConversation? Conversation { get; set; }
    public ICollection<IntegrationMessageAttachment> Attachments { get; set; } = new List<IntegrationMessageAttachment>();
}
