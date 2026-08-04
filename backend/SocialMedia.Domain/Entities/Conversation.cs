using SocialMedia.Domain.Common;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Domain.Entities;

/// <summary>
/// Messaging thread (Messenger / IG DM / WhatsApp).
/// </summary>
public class Conversation : BaseEntity
{
    public Guid SocialProfileId { get; set; }

    public string ExternalConversationId { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerImage { get; set; }
    public int UnreadCount { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public ConversationStatus Status { get; set; } = ConversationStatus.Open;

    public SocialProfile? SocialProfile { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
