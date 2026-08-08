using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Inbox;
using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Interfaces;

namespace SocialMedia.Application.Services;

public class InboxService : IInboxService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFacebookService _facebookService;
    private readonly IInstagramService _instagramService;
    private readonly IWhatsAppService _whatsAppService;

    public InboxService(
        IUnitOfWork unitOfWork,
        IFacebookService facebookService,
        IInstagramService instagramService,
        IWhatsAppService whatsAppService)
    {
        _unitOfWork = unitOfWork;
        _facebookService = facebookService;
        _instagramService = instagramService;
        _whatsAppService = whatsAppService;
    }

    public async Task<ApiResponse<IReadOnlyList<InboxItemDto>>> GetInboxAsync(Guid userId, InboxFilterRequest? filter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            Guid? platformId = null;
            if (!string.IsNullOrWhiteSpace(filter?.PlatformCode))
            {
                var platform = await _unitOfWork.Platforms.GetByCodeAsync(filter.PlatformCode, cancellationToken);
                platformId = platform?.Id;

                // WhatsApp has no comments.
                if (string.Equals(filter.PlatformCode, "whatsapp", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(filter.ItemKind, "comment", StringComparison.OrdinalIgnoreCase))
                {
                    return ApiResponse<IReadOnlyList<InboxItemDto>>.Ok(Array.Empty<InboxItemDto>());
                }
            }

            var kind = filter?.ItemKind?.ToLowerInvariant();
            var items = new List<InboxItemDto>();

            if (kind is null or "comment")
            {
                var comments = await _unitOfWork.Comments.GetByUserAsync(userId, platformId, cancellationToken);
                items.AddRange(comments.Select(c => new InboxItemDto
                {
                    Id = c.Id,
                    ItemKind = "comment",
                    PlatformCode = c.Post?.SocialProfile?.SocialAccount?.Platform?.Code ?? string.Empty,
                    ExternalId = c.ExternalCommentId,
                    AuthorName = c.AuthorName,
                    AuthorId = c.AuthorId,
                    Content = c.Message,
                    IsHidden = c.IsHidden,
                    IsRead = true,
                    IsOutgoing = !string.IsNullOrWhiteSpace(c.AuthorId) &&
                                 c.AuthorId == c.Post?.SocialProfile?.ExternalProfileId,
                    ReceivedAt = c.PlatformCreatedAt ?? c.CreatedAt,
                    CommentLikes = c.LikeCount,
                    ReplyCount = c.Replies.Count,
                    Post = c.Post is null ? null : new InboxPostMetaDto
                    {
                        PostId = c.Post.ExternalPostId ?? c.Post.Id.ToString(),
                        PageName = c.Post.SocialProfile?.Name ?? c.Post.SocialProfile?.Username ?? "Instagram",
                        PostText = FirstNonEmpty(c.Post.Caption, c.Post.Text),
                        PostImageUrl = c.Post.MediaItems.FirstOrDefault()?.Url,
                        LikesCount = c.Post.LikeCount,
                        CommentsCount = c.Post.CommentCount,
                        SharesCount = c.Post.ShareCount,
                        PostedAt = c.Post.PublishedAt ?? c.Post.CreatedAt
                    }
                }));
            }

            if (kind is null or "message")
            {
                var messages = await _unitOfWork.Messages.GetByUserAsync(userId, platformId, cancellationToken);
                items.AddRange(messages.Select(m => new InboxItemDto
                {
                    Id = m.Id,
                    ItemKind = "message",
                    PlatformCode = m.Conversation?.SocialProfile?.SocialAccount?.Platform?.Code ?? string.Empty,
                    ExternalId = m.ExternalMessageId,
                    AuthorName = m.Direction == MessageDirection.Outbound
                        ? "You"
                        : m.Conversation?.CustomerName ?? m.SenderId ?? "Instagram user",
                    AuthorId = m.SenderId,
                    Content = m.Body ?? string.Empty,
                    IsHidden = false,
                    IsRead = m.Direction == MessageDirection.Outbound || m.Conversation?.UnreadCount == 0,
                    IsOutgoing = m.Direction == MessageDirection.Outbound,
                    ConversationId = m.ConversationId,
                    ReceivedAt = m.PlatformCreatedAt ?? m.CreatedAt
                }));
            }

            return ApiResponse<IReadOnlyList<InboxItemDto>>.Ok(items.OrderByDescending(i => i.ReceivedAt).ToList());
        }
        catch (Exception ex)
        {
            return ApiResponse<IReadOnlyList<InboxItemDto>>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<object>> ReplyToCommentAsync(Guid userId, Guid commentId, ReplyCommentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var (context, code) = await ResolveCommentContextAsync(userId, commentId, cancellationToken);
            var comment = await _unitOfWork.Comments.GetByIdAsync(commentId, cancellationToken);

            if (code == "facebook")
                await _facebookService.ReplyCommentAsync(context, comment!.ExternalCommentId, request.Message, cancellationToken);
            else if (code == "instagram")
                await _instagramService.ReplyCommentAsync(context, comment!.ExternalCommentId, request.Message, cancellationToken);
            else
                return ApiResponse<object>.Fail("Platform does not support comment replies.");

            return ApiResponse<object>.Ok(new { }, "Comment reply sent.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<object>> HideCommentAsync(Guid userId, Guid commentId, HideCommentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var (context, code) = await ResolveCommentContextAsync(userId, commentId, cancellationToken);
            var comment = await _unitOfWork.Comments.GetByIdAsync(commentId, cancellationToken);

            if (code == "facebook")
                await _facebookService.HideCommentAsync(context, comment!.ExternalCommentId, request.Hide, cancellationToken);
            else if (code == "instagram")
                await _instagramService.HideCommentAsync(context, comment!.ExternalCommentId, request.Hide, cancellationToken);
            else
                return ApiResponse<object>.Fail("Platform does not support hiding comments.");

            comment!.IsHidden = request.Hide;
            _unitOfWork.Comments.Update(comment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<object>.Ok(new { }, request.Hide ? "Comment hidden." : "Comment unhidden.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<object>> DeleteCommentAsync(Guid userId, Guid commentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var (context, code) = await ResolveCommentContextAsync(userId, commentId, cancellationToken);
            var comment = await _unitOfWork.Comments.GetByIdAsync(commentId, cancellationToken);

            if (code == "facebook")
                await _facebookService.DeleteCommentAsync(context, comment!.ExternalCommentId, cancellationToken);
            else if (code == "instagram")
                await _instagramService.DeleteCommentAsync(context, comment!.ExternalCommentId, cancellationToken);
            else
                return ApiResponse<object>.Fail("Platform does not support deleting comments.");

            comment!.IsDeleted = true;
            _unitOfWork.Comments.Update(comment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<object>.Ok(new { }, "Comment deleted.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<object>> ReplyToMessageAsync(Guid userId, Guid messageId, ReplyMessageRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var (context, code, recipientId) = await ResolveMessageContextAsync(userId, messageId, cancellationToken);

            if (code == "facebook")
                await _facebookService.SendMessageAsync(context, recipientId, request.Message, cancellationToken);
            else if (code == "instagram")
                await _instagramService.SendMessageAsync(context, recipientId, request.Message, cancellationToken);
            else if (code == "whatsapp")
                await _whatsAppService.SendMessageAsync(context, recipientId, request.Message, cancellationToken);
            else
                return ApiResponse<object>.Fail("Platform does not support messaging.");

            return ApiResponse<object>.Ok(new { }, "Message sent.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<object>> DeleteMessageAsync(Guid userId, Guid messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = await _unitOfWork.Messages.GetByIdAsync(messageId, cancellationToken);
            if (message is null)
                return ApiResponse<object>.Fail("Message not found.");

            var conversation = await _unitOfWork.Conversations.GetByIdAsync(message.ConversationId, cancellationToken);
            var profile = conversation is null ? null : await _unitOfWork.SocialProfiles.GetByIdAsync(conversation.SocialProfileId, cancellationToken);
            var account = profile is null ? null : await _unitOfWork.SocialAccounts.GetWithAuthAndProfilesAsync(profile.SocialAccountId, cancellationToken);
            if (account is null || account.UserId != userId)
                return ApiResponse<object>.Fail("Message not found.");

            _unitOfWork.Messages.Remove(message);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<object>.Ok(new { }, "Message deleted.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<object>> MarkReadAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var conversation = await _unitOfWork.Conversations.GetByIdAsync(conversationId, cancellationToken);
            if (conversation is null)
                return ApiResponse<object>.Fail("Conversation not found.");

            var profile = await _unitOfWork.SocialProfiles.GetByIdAsync(conversation.SocialProfileId, cancellationToken);
            var account = profile is null ? null : await _unitOfWork.SocialAccounts.GetByIdAsync(profile.SocialAccountId, cancellationToken);
            if (account is null || account.UserId != userId)
                return ApiResponse<object>.Fail("Conversation not found.");

            conversation.UnreadCount = 0;
            conversation.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Conversations.Update(conversation);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<object>.Ok(new { }, "Marked as read.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private async Task<(MetaCallContext Context, string Code)> ResolveCommentContextAsync(Guid userId, Guid commentId, CancellationToken cancellationToken)
    {
        var comment = await _unitOfWork.Comments.GetByIdAsync(commentId, cancellationToken)
            ?? throw new InvalidOperationException("Comment not found.");
        var post = await _unitOfWork.Posts.GetByIdAsync(comment.PostId, cancellationToken)
            ?? throw new InvalidOperationException("Post not found.");
        var profile = await _unitOfWork.SocialProfiles.GetByIdAsync(post.SocialProfileId, cancellationToken)
            ?? throw new InvalidOperationException("Profile not found.");
        var account = await _unitOfWork.SocialAccounts.GetWithAuthAndProfilesAsync(profile.SocialAccountId, cancellationToken)
            ?? throw new InvalidOperationException("Account not found.");
        if (account.UserId != userId || account.Auth is null)
            throw new InvalidOperationException("Comment not found.");

        var platform = await _unitOfWork.Platforms.GetByIdAsync(account.PlatformId, cancellationToken);
        return (new MetaCallContext
        {
            AccessToken = account.Auth.AccessToken,
            ProfileExternalId = profile.ExternalProfileId,
            PageExternalId = ReadPageId(profile.MetadataJson)
        }, platform?.Code?.ToLowerInvariant() ?? string.Empty);
    }

    private async Task<(MetaCallContext Context, string Code, string RecipientId)> ResolveMessageContextAsync(Guid userId, Guid messageId, CancellationToken cancellationToken)
    {
        var message = await _unitOfWork.Messages.GetByIdAsync(messageId, cancellationToken)
            ?? throw new InvalidOperationException("Message not found.");
        var conversation = await _unitOfWork.Conversations.GetByIdAsync(message.ConversationId, cancellationToken)
            ?? throw new InvalidOperationException("Conversation not found.");
        var profile = await _unitOfWork.SocialProfiles.GetByIdAsync(conversation.SocialProfileId, cancellationToken)
            ?? throw new InvalidOperationException("Profile not found.");
        var account = await _unitOfWork.SocialAccounts.GetWithAuthAndProfilesAsync(profile.SocialAccountId, cancellationToken)
            ?? throw new InvalidOperationException("Account not found.");
        if (account.UserId != userId || account.Auth is null)
            throw new InvalidOperationException("Message not found.");

        var recipient = (message.Direction == MessageDirection.Outbound
            ? message.ReceiverId ?? conversation.CustomerId
            : message.SenderId ?? conversation.CustomerId)
            ?? throw new InvalidOperationException("Recipient unknown.");

        var platform = await _unitOfWork.Platforms.GetByIdAsync(account.PlatformId, cancellationToken);
        return (new MetaCallContext
        {
            AccessToken = account.Auth.AccessToken,
            ProfileExternalId = profile.ExternalProfileId,
            PageExternalId = ReadPageId(profile.MetadataJson)
        }, platform?.Code?.ToLowerInvariant() ?? string.Empty, recipient);
    }

    private static string? ReadPageId(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(metadataJson);
            return doc.RootElement.TryGetProperty("pageId", out var pageId) ? pageId.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
