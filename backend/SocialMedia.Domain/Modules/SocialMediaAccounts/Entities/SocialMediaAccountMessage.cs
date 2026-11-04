using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.SocialMediaAccounts.Entities;

public class SocialMediaAccountMessage : MessageEntityBase
{
    public SocialMediaAccountConversation? Conversation { get; set; }
    public ICollection<SocialMediaAccountMessageAttachment> Attachments { get; set; } = new List<SocialMediaAccountMessageAttachment>();
}
