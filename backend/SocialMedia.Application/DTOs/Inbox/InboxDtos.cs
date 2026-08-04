namespace SocialMedia.Application.DTOs.Inbox;

public class InboxItemDto
{
    public Guid Id { get; set; }
    public string ItemKind { get; set; } = string.Empty; // comment | message
    public string PlatformCode { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsHidden { get; set; }
    public bool IsRead { get; set; }
    public DateTime ReceivedAt { get; set; }
}

public class InboxFilterRequest
{
    public string? PlatformCode { get; set; }
    public string? ItemKind { get; set; } // comment | message
}

public class ReplyCommentRequest
{
    public string Message { get; set; } = string.Empty;
}

public class ReplyMessageRequest
{
    public string Message { get; set; } = string.Empty;
}

public class HideCommentRequest
{
    public bool Hide { get; set; } = true;
}
