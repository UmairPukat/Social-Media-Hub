using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.SocialMediaAccounts.Entities;

public class SocialMediaAccountConversation : ConversationEntityBase
{
    public SocialMediaAccountSocialProfile? SocialProfile { get; set; }
    public ICollection<SocialMediaAccountMessage> Messages { get; set; } = new List<SocialMediaAccountMessage>();
}
