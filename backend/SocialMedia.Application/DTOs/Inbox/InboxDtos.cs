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
    public bool IsOutgoing { get; set; }
    public Guid? ConversationId { get; set; }
    public DateTime ReceivedAt { get; set; }
    public InboxPostMetaDto? Post { get; set; }
    public int CommentLikes { get; set; }
    public int ReplyCount { get; set; }

    /// <summary>Set on a comment reply so the Inbox can nest it under the comment it answers.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Quoted message this one replies to, shown above the bubble like Messenger.</summary>
    public Guid? ReplyToId { get; set; }
    public string? ReplyToAuthor { get; set; }
    public string? ReplyToContent { get; set; }

    /// <summary>integration or app_connection — which menu owns the connected account.</summary>
    public string? MenuType { get; set; }

    /// <summary>Facebook Page external id used for Messenger replies.</summary>
    public string? PageId { get; set; }

    /// <summary>Instagram account external id used for Instagram DM replies.</summary>
    public string? AccountId { get; set; }
}

public class InboxPostMetaDto
{
    public string PostId { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;
    public string PostText { get; set; } = string.Empty;
    public string? PostImageUrl { get; set; }
    public int LikesCount { get; set; }
    public int CommentsCount { get; set; }
    public int SharesCount { get; set; }
    public DateTime PostedAt { get; set; }
}

public class InboxFilterRequest
{
    public string? PlatformCode { get; set; }
    public string? ItemKind { get; set; } // comment | message
    public string? MenuType { get; set; }
}

public class ReplyCommentRequest
{
    public string Message { get; set; } = string.Empty;
    public string? MenuType { get; set; }
    public string? PageId { get; set; }
    public string? AccountId { get; set; }
}

public class ReplyMessageRequest
{
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional message being quoted, sent to Meta as reply_to.mid.</summary>
    public Guid? ReplyToMessageId { get; set; }

    public string? MenuType { get; set; }
    public string? PageId { get; set; }
    public string? AccountId { get; set; }
}

public class HideCommentRequest
{
    public bool Hide { get; set; } = true;
}
