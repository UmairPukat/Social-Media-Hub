using SocialMedia.Domain.Common;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Domain.Modules.Common.Entities;

public abstract class MessageEntityBase : BaseEntity
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
    public Guid? ReplyToMessageId { get; set; }
    public string? ReplyToExternalId { get; set; }
}
