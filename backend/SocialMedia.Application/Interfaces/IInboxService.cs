using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Inbox;

namespace SocialMedia.Application.Interfaces;

public interface IInboxService
{
    Task<ApiResponse<IReadOnlyList<InboxItemDto>>> GetInboxAsync(Guid userId, InboxFilterRequest? filter = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> ReplyToCommentAsync(Guid userId, Guid commentId, ReplyCommentRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> HideCommentAsync(Guid userId, Guid commentId, HideCommentRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> DeleteCommentAsync(Guid userId, Guid commentId, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> ReplyToMessageAsync(Guid userId, Guid messageId, ReplyMessageRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> DeleteMessageAsync(Guid userId, Guid messageId, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> MarkReadAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default);
}
