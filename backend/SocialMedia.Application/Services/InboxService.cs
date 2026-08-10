using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Inbox;
using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Interfaces;

namespace SocialMedia.Application.Services;

public class InboxService : IInboxService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFacebookService _facebookService;
    private readonly IInstagramService _instagramService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IInboxRealtimeNotifier _inboxRealtime;

    public InboxService(
        IUnitOfWork unitOfWork,
        IFacebookService facebookService,
        IInstagramService instagramService,
        IWhatsAppService whatsAppService,
        IInboxRealtimeNotifier inboxRealtime)
    {
        _unitOfWork = unitOfWork;
        _facebookService = facebookService;
        _instagramService = instagramService;
        _whatsAppService = whatsAppService;
        _inboxRealtime = inboxRealtime;
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
                    ParentId = c.ParentCommentId,
                    Post = c.Post is null ? null : new InboxPostMetaDto
                    {
                        PostId = c.Post.ExternalPostId ?? c.Post.Id.ToString(),
                        PageName = c.Post.SocialProfile?.Name ?? c.Post.SocialProfile?.Username ?? "Instagram",
                        PostText = DisplayPostText(c.Post),
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
                var byId = messages.ToDictionary(m => m.Id);
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
                    ReceivedAt = m.PlatformCreatedAt ?? m.CreatedAt,
                    ReplyToId = m.ReplyToMessageId,
                    ReplyToAuthor = QuotedAuthor(m, byId),
                    ReplyToContent = m.ReplyToMessageId.HasValue && byId.TryGetValue(m.ReplyToMessageId.Value, out var quoted)
                        ? quoted.Body
                        : null
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
            if (string.IsNullOrWhiteSpace(request.Message))
                return ApiResponse<object>.Fail("Message is required.");

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
            var code = platform?.Code?.ToLowerInvariant() ?? string.Empty;
            if (code is not ("facebook" or "instagram"))
                return ApiResponse<object>.Fail("Platform does not support comment replies.");

            // Prefer a real Meta comment id. Local optimistic rows are skipped.
            var replyTargetExternalId = await ResolveReplyTargetExternalIdAsync(comment, code, cancellationToken);
            if (string.IsNullOrWhiteSpace(replyTargetExternalId) ||
                replyTargetExternalId.StartsWith("local_reply_", StringComparison.OrdinalIgnoreCase) ||
                replyTargetExternalId.StartsWith("local_", StringComparison.OrdinalIgnoreCase))
                return ApiResponse<object>.Fail("Cannot reply until the original comment is synced from Meta.");

            var tokens = CandidateTokens(account.Auth);
            if (tokens.Count == 0)
                return ApiResponse<object>.Fail("No access token is available. Reconnect the account.");

            string? remoteCommentId = null;
            Exception? lastError = null;
            foreach (var token in tokens)
            {
                var context = new MetaCallContext
                {
                    AccessToken = token,
                    ProfileExternalId = profile.ExternalProfileId,
                    PageExternalId = ReadPageId(profile.MetadataJson)
                };

                try
                {
                    remoteCommentId = code == "facebook"
                        ? await _facebookService.ReplyCommentAsync(context, replyTargetExternalId, request.Message.Trim(), cancellationToken)
                        : await _instagramService.ReplyCommentAsync(context, replyTargetExternalId, request.Message.Trim(), cancellationToken);
                    lastError = null;
                    break;
                }
                catch (Exception ex) when (IsOAuthTokenError(ex))
                {
                    lastError = ex;
                }
            }

            if (lastError is not null)
                return ApiResponse<object>.Fail(lastError.Message);

            var externalId = string.IsNullOrWhiteSpace(remoteCommentId)
                ? $"local_reply_{Guid.NewGuid():N}"
                : remoteCommentId!;

            // Avoid duplicates when Meta immediately echoes the reply through webhooks.
            var existing = await _unitOfWork.Comments.GetByExternalCommentIdAsync(externalId, cancellationToken);
            if (existing is not null)
            {
                await _inboxRealtime.NotifyInboxItemAsync(userId, MapCommentInboxItem(existing, post, profile, code, isOutgoing: true), cancellationToken);
                return ApiResponse<object>.Ok(new { replyId = existing.Id }, "Comment reply sent.");
            }

            var reply = new Comment
            {
                PostId = post.Id,
                ParentCommentId = comment.ParentCommentId ?? comment.Id,
                ExternalCommentId = externalId,
                AuthorId = profile.ExternalProfileId,
                AuthorName = profile.Name ?? profile.Username ?? "You",
                Message = request.Message.Trim(),
                PlatformCreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Comments.AddAsync(reply, cancellationToken);
            post.CommentCount += 1;
            post.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Posts.Update(post);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _inboxRealtime.NotifyInboxItemAsync(
                userId,
                MapCommentInboxItem(reply, post, profile, code, isOutgoing: true),
                cancellationToken);

            return ApiResponse<object>.Ok(new { replyId = reply.Id }, "Comment reply sent.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Page token first, then long-lived user token (RefreshToken). Meta may invalidate one while
    /// the other still works ("session is invalid because the user logged out").
    /// </summary>
    private static List<string> CandidateTokens(SocialAuth auth)
    {
        var tokens = new List<string>();
        void Add(string? token)
        {
            if (!string.IsNullOrWhiteSpace(token) && !tokens.Contains(token))
                tokens.Add(token!);
        }

        Add(auth.AccessToken);
        Add(auth.RefreshToken);
        return tokens;
    }

    private static bool IsOAuthTokenError(Exception ex)
    {
        var text = ex.Message ?? string.Empty;
        return text.Contains("OAuthException", StringComparison.OrdinalIgnoreCase)
            || text.Contains("\"code\":190", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Error validating access token", StringComparison.OrdinalIgnoreCase)
            || text.Contains("session is invalid", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Instagram only allows replies on top-level comments. For both platforms, skip local
    /// optimistic ids and walk to the nearest Meta comment id.
    /// </summary>
    private async Task<string> ResolveReplyTargetExternalIdAsync(
        Comment comment,
        string platformCode,
        CancellationToken cancellationToken)
    {
        var current = comment;
        if (platformCode == "instagram")
        {
            var guard = 0;
            while (current.ParentCommentId.HasValue && guard++ < 20)
            {
                var parent = await _unitOfWork.Comments.GetByIdAsync(current.ParentCommentId.Value, cancellationToken);
                if (parent is null) break;
                current = parent;
            }
        }

        var walk = current;
        var hops = 0;
        while (hops++ < 20)
        {
            var id = walk.ExternalCommentId ?? string.Empty;
            if (!IsLocalExternalId(id))
                return id;

            if (!walk.ParentCommentId.HasValue)
                return string.Empty;

            var parent = await _unitOfWork.Comments.GetByIdAsync(walk.ParentCommentId.Value, cancellationToken);
            if (parent is null)
                return string.Empty;
            walk = parent;
        }

        return string.Empty;
    }

    private static bool IsLocalExternalId(string id)
        => id.StartsWith("local_reply_", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("local_", StringComparison.OrdinalIgnoreCase);

    private InboxItemDto MapCommentInboxItem(
        Comment comment,
        Post post,
        SocialProfile profile,
        string platformCode,
        bool isOutgoing)
        => new()
        {
            Id = comment.Id,
            ItemKind = "comment",
            PlatformCode = platformCode,
            ExternalId = comment.ExternalCommentId,
            AuthorName = comment.AuthorName,
            AuthorId = comment.AuthorId,
            Content = comment.Message,
            IsHidden = comment.IsHidden,
            IsRead = true,
            IsOutgoing = isOutgoing,
            ReceivedAt = comment.PlatformCreatedAt ?? comment.CreatedAt,
            CommentLikes = comment.LikeCount,
            ReplyCount = 0,
            ParentId = comment.ParentCommentId,
            Post = new InboxPostMetaDto
            {
                PostId = post.ExternalPostId ?? post.Id.ToString(),
                PageName = profile.Name ?? profile.Username ?? platformCode,
                PostText = DisplayPostText(post),
                PostImageUrl = post.MediaItems.FirstOrDefault()?.Url,
                LikesCount = post.LikeCount,
                CommentsCount = post.CommentCount,
                SharesCount = post.ShareCount,
                PostedAt = post.PublishedAt ?? post.CreatedAt
            }
        };

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
            if (string.IsNullOrWhiteSpace(request.Message))
                return ApiResponse<object>.Fail("Message is required.");

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

            var platform = await _unitOfWork.Platforms.GetByIdAsync(account.PlatformId, cancellationToken);
            var code = platform?.Code?.ToLowerInvariant() ?? string.Empty;
            if (code is not ("facebook" or "instagram" or "whatsapp"))
                return ApiResponse<object>.Fail("Platform does not support messaging.");

            var recipientId = (message.Direction == MessageDirection.Outbound
                ? message.ReceiverId ?? conversation.CustomerId
                : message.SenderId ?? conversation.CustomerId) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(recipientId))
                return ApiResponse<object>.Fail("Recipient is unknown for this conversation.");

            // A quoted reply must point at a message from the same conversation.
            Message? quoted = null;
            if (request.ReplyToMessageId.HasValue)
            {
                quoted = await _unitOfWork.Messages.GetByIdAsync(request.ReplyToMessageId.Value, cancellationToken);
                if (quoted is null || quoted.ConversationId != conversation.Id)
                    return ApiResponse<object>.Fail("The message being replied to is not part of this conversation.");
            }

            var replyToMid = quoted is not null && !IsLocalExternalId(quoted.ExternalMessageId)
                ? quoted.ExternalMessageId
                : null;

            var tokens = CandidateTokens(account.Auth);
            if (tokens.Count == 0)
                return ApiResponse<object>.Fail("No access token is available. Reconnect the account.");

            string? remoteMessageId = null;
            Exception? lastError = null;
            foreach (var token in tokens)
            {
                var context = new MetaCallContext
                {
                    AccessToken = token,
                    ProfileExternalId = profile.ExternalProfileId,
                    PageExternalId = ReadPageId(profile.MetadataJson)
                };

                try
                {
                    remoteMessageId = code switch
                    {
                        "facebook" => await _facebookService.SendMessageAsync(context, recipientId, request.Message.Trim(), replyToMid, cancellationToken),
                        "instagram" => await _instagramService.SendMessageAsync(context, recipientId, request.Message.Trim(), replyToMid, cancellationToken),
                        "whatsapp" => await _whatsAppService.SendMessageAsync(context, recipientId, request.Message.Trim(), replyToMid, cancellationToken),
                        _ => null
                    };
                    lastError = null;
                    break;
                }
                catch (Exception ex) when (IsOAuthTokenError(ex))
                {
                    lastError = ex;
                }
            }

            if (lastError is not null)
                return ApiResponse<object>.Fail(lastError.Message);

            var externalId = string.IsNullOrWhiteSpace(remoteMessageId)
                ? $"local_msg_{Guid.NewGuid():N}"
                : remoteMessageId!;

            var existing = await _unitOfWork.Messages.GetByExternalMessageIdAsync(externalId, cancellationToken);
            if (existing is not null)
            {
                await _inboxRealtime.NotifyInboxItemAsync(
                    userId,
                    MapMessageInboxItem(existing, conversation, code, quoted),
                    cancellationToken);
                return ApiResponse<object>.Ok(new { messageId = existing.Id }, "Message sent.");
            }

            var sentAt = DateTime.UtcNow;
            var outbound = new Message
            {
                ConversationId = conversation.Id,
                ExternalMessageId = externalId,
                SenderId = profile.ExternalProfileId,
                ReceiverId = recipientId,
                Direction = MessageDirection.Outbound,
                MessageType = MessageContentType.Text,
                Body = request.Message.Trim(),
                Status = MessageDeliveryStatus.Sent,
                PlatformCreatedAt = sentAt,
                ReplyToMessageId = quoted?.Id,
                ReplyToExternalId = replyToMid
            };
            await _unitOfWork.Messages.AddAsync(outbound, cancellationToken);

            conversation.LastMessageAt = sentAt;
            conversation.UpdatedAt = sentAt;
            _unitOfWork.Conversations.Update(conversation);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _inboxRealtime.NotifyInboxItemAsync(
                userId,
                MapMessageInboxItem(outbound, conversation, code, quoted),
                cancellationToken);

            return ApiResponse<object>.Ok(new { messageId = outbound.Id }, "Message sent.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    private static InboxItemDto MapMessageInboxItem(
        Message message,
        Conversation conversation,
        string platformCode,
        Message? quoted = null)
        => new()
        {
            Id = message.Id,
            ItemKind = "message",
            PlatformCode = platformCode,
            ExternalId = message.ExternalMessageId,
            AuthorName = message.Direction == MessageDirection.Outbound
                ? "You"
                : conversation.CustomerName ?? message.SenderId ?? "User",
            AuthorId = message.SenderId,
            Content = message.Body ?? string.Empty,
            IsHidden = false,
            IsRead = true,
            IsOutgoing = message.Direction == MessageDirection.Outbound,
            ConversationId = conversation.Id,
            ReceivedAt = message.PlatformCreatedAt ?? message.CreatedAt,
            ReplyToId = quoted?.Id ?? message.ReplyToMessageId,
            ReplyToAuthor = quoted is null
                ? null
                : quoted.Direction == MessageDirection.Outbound ? "You" : conversation.CustomerName ?? quoted.SenderId,
            ReplyToContent = quoted?.Body
        };

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

    private static string? QuotedAuthor(Message message, IReadOnlyDictionary<Guid, Message> byId)
    {
        if (!message.ReplyToMessageId.HasValue || !byId.TryGetValue(message.ReplyToMessageId.Value, out var quoted))
            return null;

        return quoted.Direction == MessageDirection.Outbound
            ? "You"
            : quoted.Conversation?.CustomerName ?? quoted.SenderId;
    }

    private static string DisplayPostText(Post post)
    {
        var text = FirstNonEmpty(post.Caption, post.Text);
        return !string.IsNullOrWhiteSpace(post.ExternalPostId) &&
               string.Equals(text, $"Facebook post {post.ExternalPostId}", StringComparison.Ordinal)
            ? "Facebook post"
            : text;
    }

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
