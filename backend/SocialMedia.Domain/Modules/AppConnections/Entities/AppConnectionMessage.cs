using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AppConnections.Entities;

public class AppConnectionMessage : MessageEntityBase
{
    public AppConnectionConversation? Conversation { get; set; }
    public ICollection<AppConnectionMessageAttachment> Attachments { get; set; } = new List<AppConnectionMessageAttachment>();
}
