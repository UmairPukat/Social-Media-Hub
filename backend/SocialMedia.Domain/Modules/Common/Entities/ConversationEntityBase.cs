using SocialMedia.Domain.Common;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Domain.Modules.Common.Entities;

public abstract class ConversationEntityBase : BaseEntity
{
    public Guid SocialProfileId { get; set; }
    public string ExternalConversationId { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerImage { get; set; }
    public int UnreadCount { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public ConversationStatus Status { get; set; } = ConversationStatus.Open;
}
