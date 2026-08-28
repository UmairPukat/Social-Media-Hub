using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.Integrations.Entities;

public class IntegrationConversation : ConversationEntityBase
{
    public IntegrationSocialProfile? SocialProfile { get; set; }
    public ICollection<IntegrationMessage> Messages { get; set; } = new List<IntegrationMessage>();
}
