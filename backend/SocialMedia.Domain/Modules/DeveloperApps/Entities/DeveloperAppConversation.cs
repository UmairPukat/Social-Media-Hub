using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.DeveloperApps.Entities;

public class DeveloperAppConversation : ConversationEntityBase
{
    public DeveloperAppSocialProfile? SocialProfile { get; set; }
    public ICollection<DeveloperAppMessage> Messages { get; set; } = new List<DeveloperAppMessage>();
}
