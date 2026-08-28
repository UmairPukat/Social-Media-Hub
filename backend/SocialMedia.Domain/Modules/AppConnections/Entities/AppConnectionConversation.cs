using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AppConnections.Entities;

public class AppConnectionConversation : ConversationEntityBase
{
    public AppConnectionSocialProfile? SocialProfile { get; set; }
    public ICollection<AppConnectionMessage> Messages { get; set; } = new List<AppConnectionMessage>();
}
