using SocialMedia.Domain.Common;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Domain.Entities;

/// <summary>
/// Single message inside a Conversation.
/// </summary>
public class Message : BaseEntity
{
    public Guid ConversationId { get; set; }

    public string ExternalMessageId { get; set; } = string.Empty;
    public string? SenderId { get; set; }
    public string? ReceiverId { get; set; }
    public MessageDirection Direction { get; set; }
    public MessageContentType MessageType { get; set; } = MessageContentType.Text;
    public string? Body { get; set; }
    public MessageDeliveryStatus Status { get; set; } = MessageDeliveryStatus.Pending;
    public DateTime? PlatformCreatedAt { get; set; }

    public Conversation? Conversation { get; set; }
    public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
}
