using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialMedia.Application.DTOs.Inbox;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Settings;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Interfaces;

namespace SocialMedia.Infrastructure.Meta;

/// <summary>
/// Facebook Graph API. OAuth auth URLs are built on the frontend.
/// </summary>
public class FacebookService : IFacebookService
{
    private readonly MetaGraphClient _graph;
    private readonly FacebookSettings _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInboxRealtimeNotifier _inboxRealtime;
    private readonly ILogger<FacebookService> _logger;

    public FacebookService(
        MetaGraphClient graph,
        IOptions<MetaSettings> options,
        IUnitOfWork unitOfWork,
        IInboxRealtimeNotifier inboxRealtime,
        ILogger<FacebookService> logger)
    {
        _graph = graph;
        _settings = options.Value.Facebook;
        _unitOfWork = unitOfWork;
        _inboxRealtime = inboxRealtime;
        _logger = logger;
    }

    public async Task<OAuthTokenResult> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        using var shortLived = await _graph.GetAsync(
            _settings.GraphApiVersion, "oauth/access_token", string.Empty, cancellationToken,
            ("client_id", _settings.AppId),
            ("client_secret", _settings.AppSecret),
            ("redirect_uri", redirectUri),
            ("code", code));

        var shortToken = shortLived.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Meta did not return an access token.");

        // Upgrade to a long-lived user token when possible.
        try
        {
            using var longLived = await _graph.GetAsync(
                _settings.GraphApiVersion, "oauth/access_token", string.Empty, cancellationToken,
                ("grant_type", "fb_exchange_token"),
                ("client_id", _settings.AppId),
                ("client_secret", _settings.AppSecret),
                ("fb_exchange_token", shortToken));

            return ParseToken(longLived.RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Long-lived token exchange failed; using short-lived token.");
            return ParseToken(shortLived.RootElement);
        }
    }

    public async Task<(string Id, string Name)> GetMeAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var doc = await _graph.GetAsync(
            _settings.GraphApiVersion, "me", accessToken, cancellationToken,
            ("fields", "id,name"));

        var id = doc.RootElement.GetProperty("id").GetString() ?? string.Empty;
        var name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? "Facebook User" : "Facebook User";
        return (id, name);
    }

    private static OAuthTokenResult ParseToken(JsonElement root)
    {
        var token = root.GetProperty("access_token").GetString() ?? string.Empty;
        DateTime? expires = null;
        if (root.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var seconds))
            expires = DateTime.UtcNow.AddSeconds(seconds);

        return new OAuthTokenResult
        {
            AccessToken = token,
            ExpiresAt = expires,
            TokenType = root.TryGetProperty("token_type", out var tt) ? tt.GetString() : null
        };
    }

    public async Task<IReadOnlyList<SocialProfileDraft>> DiscoverProfilesAsync(string userAccessToken, CancellationToken cancellationToken = default)
    {
        using var pagesDoc = await _graph.GetAsync(
            _settings.GraphApiVersion, "me/accounts", userAccessToken, cancellationToken,
            ("fields", "id,name,access_token,picture"));

        var list = new List<SocialProfileDraft>();
        if (!pagesDoc.RootElement.TryGetProperty("data", out var data))
            return list;

        foreach (var page in data.EnumerateArray())
        {
            list.Add(new SocialProfileDraft
            {
                ExternalProfileId = page.GetProperty("id").GetString() ?? string.Empty,
                Name = page.TryGetProperty("name", out var n) ? n.GetString() ?? "Facebook Page" : "Facebook Page",
                ProfileType = "FacebookPage",
                PageAccessToken = page.TryGetProperty("access_token", out var t) ? t.GetString() : null
            });
        }

        return list;
    }

    public Task<IReadOnlyList<MetaPageInfo>> ListPagesAsync(string userAccessToken, CancellationToken cancellationToken = default)
        => _graph.ListPagesAsync(_settings.GraphApiVersion, userAccessToken, cancellationToken);

    /// <summary>Subscribe the selected page to feed and messaging webhook fields.</summary>
    public Task SubscribePageWebhooksAsync(string pageId, string pageAccessToken, CancellationToken cancellationToken = default)
        => _graph.SubscribePageAsync(
            _settings.GraphApiVersion, pageId, pageAccessToken, MetaGraphClient.PageSubscribedFields, cancellationToken);

    public Task UnsubscribePageWebhooksAsync(string pageId, string pageAccessToken, CancellationToken cancellationToken = default)
        => _graph.UnsubscribePageAsync(_settings.GraphApiVersion, pageId, pageAccessToken, cancellationToken);

    public Task<IReadOnlyList<string>> GetSubscribedFieldsAsync(string pageId, string pageAccessToken, CancellationToken cancellationToken = default)
        => _graph.GetPageSubscribedFieldsAsync(_settings.GraphApiVersion, pageId, pageAccessToken, cancellationToken);

    public async Task<PostDto> CreatePostAsync(MetaCallContext context, string content, string? mediaUrl, CancellationToken cancellationToken = default)
    {
        var fields = new Dictionary<string, string> { ["message"] = content };
        if (!string.IsNullOrWhiteSpace(mediaUrl))
            fields["link"] = mediaUrl;

        using var doc = await _graph.PostAsync(_settings.GraphApiVersion, $"{context.ProfileExternalId}/feed", context.AccessToken, fields, cancellationToken);
        return new PostDto { Id = doc.RootElement.GetProperty("id").GetString() ?? string.Empty, Message = content, CreatedTime = DateTime.UtcNow };
    }

    public async Task<IReadOnlyList<PostDto>> GetPostsAsync(MetaCallContext context, CancellationToken cancellationToken = default)
    {
        using var doc = await _graph.GetAsync(_settings.GraphApiVersion, $"{context.ProfileExternalId}/feed", context.AccessToken, cancellationToken,
            ("fields", "id,message,permalink_url,created_time"), ("limit", "25"));
        return ParsePosts(doc);
    }

    public async Task<RemotePostSnapshot?> GetPostSnapshotAsync(string pageAccessToken, string postId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pageAccessToken) || string.IsNullOrWhiteSpace(postId))
            return null;

        using var doc = await _graph.GetAsync(_settings.GraphApiVersion, postId, pageAccessToken, cancellationToken,
            ("fields", "id,message,story,permalink_url,created_time,full_picture,shares,likes.summary(true),comments.summary(true)"));

        var root = doc.RootElement;
        return new RemotePostSnapshot
        {
            ExternalId = root.TryGetProperty("id", out var id) ? id.GetString() ?? postId : postId,
            Text = FirstNonEmpty(
                root.TryGetProperty("message", out var message) ? message.GetString() : null,
                root.TryGetProperty("story", out var story) ? story.GetString() : null),
            Permalink = root.TryGetProperty("permalink_url", out var permalink) ? permalink.GetString() : null,
            MediaUrl = root.TryGetProperty("full_picture", out var picture) ? picture.GetString() : null,
            LikeCount = ReadSummaryCount(root, "likes"),
            CommentCount = ReadSummaryCount(root, "comments"),
            ShareCount = root.TryGetProperty("shares", out var shares) &&
                         shares.TryGetProperty("count", out var shareCount) &&
                         shareCount.TryGetInt32(out var shareValue)
                ? shareValue
                : 0,
            CreatedTime = root.TryGetProperty("created_time", out var created) &&
                          DateTime.TryParse(created.GetString(), out var createdAt)
                ? createdAt.ToUniversalTime()
                : null
        };
    }

    private static int ReadSummaryCount(JsonElement root, string edge)
        => root.TryGetProperty(edge, out var element) &&
           element.TryGetProperty("summary", out var summary) &&
           summary.TryGetProperty("total_count", out var total) &&
           total.TryGetInt32(out var count)
            ? count
            : 0;

    public async Task<IReadOnlyList<CommentDto>> GetCommentsAsync(MetaCallContext context, string postId, CancellationToken cancellationToken = default)
    {
        using var doc = await _graph.GetAsync(_settings.GraphApiVersion, $"{postId}/comments", context.AccessToken, cancellationToken,
            ("fields", "id,message,from,created_time,is_hidden"), ("limit", "50"));
        return ParseComments(doc);
    }

    public async Task ReplyCommentAsync(MetaCallContext context, string commentId, string message, CancellationToken cancellationToken = default)
    {
        using var _ = await _graph.PostAsync(_settings.GraphApiVersion, $"{commentId}/comments", context.AccessToken,
            new Dictionary<string, string> { ["message"] = message }, cancellationToken);
    }

    public async Task HideCommentAsync(MetaCallContext context, string commentId, bool hide, CancellationToken cancellationToken = default)
    {
        using var _ = await _graph.PostAsync(_settings.GraphApiVersion, commentId, context.AccessToken,
            new Dictionary<string, string> { ["is_hidden"] = hide ? "true" : "false" }, cancellationToken);
    }

    public Task DeleteCommentAsync(MetaCallContext context, string commentId, CancellationToken cancellationToken = default)
        => _graph.DeleteAsync(_settings.GraphApiVersion, commentId, context.AccessToken, cancellationToken);

    public async Task SendMessageAsync(MetaCallContext context, string recipientId, string message, CancellationToken cancellationToken = default)
    {
        var payload = new { recipient = new { id = recipientId }, messaging_type = "RESPONSE", message = new { text = message } };
        using var _ = await _graph.PostJsonAsync(_settings.GraphApiVersion, $"{context.ProfileExternalId}/messages", context.AccessToken, payload, cancellationToken);
    }

    public Task DeleteMessageAsync(MetaCallContext context, string messageId, CancellationToken cancellationToken = default)
        => _graph.DeleteAsync(_settings.GraphApiVersion, messageId, context.AccessToken, cancellationToken);

    /// <summary>
    /// Page webhooks: <c>changes[field=feed]</c> carries posts, comments, and reactions;
    /// <c>messaging[]</c> carries Messenger threads. Rows are persisted, then pushed to the
    /// connected Angular client over SignalR.
    /// </summary>
    public async Task<WebhookProcessResult> ProcessWebhookPayloadAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        var result = new WebhookProcessResult();
        try
        {
            using var doc = JsonDocument.Parse(webhookEvent.PayloadJson);
            if (!doc.RootElement.TryGetProperty("entry", out var entries))
            {
                result.Skip("Payload has no 'entry' array — not a Meta page webhook delivery.");
                return result;
            }

            foreach (var entry in entries.EnumerateArray())
            {
                var pageId = entry.TryGetProperty("id", out var idElement) ? idElement.ToString() : null;
                if (string.IsNullOrWhiteSpace(pageId))
                {
                    result.Skip("Entry has no page id.");
                    continue;
                }

                var profile = await ResolveProfileAsync(pageId!, result, cancellationToken);
                if (profile is null)
                    continue;

                var account = await _unitOfWork.SocialAccounts.GetByIdAsync(profile.SocialAccountId, cancellationToken);
                if (account is null)
                {
                    result.Skip($"Page '{pageId}' has no owning account.");
                    continue;
                }

                if (entry.TryGetProperty("changes", out var changes))
                    await ProcessChangesAsync(profile, account, entry, changes, result, cancellationToken);

                // Messenger threads arrive as a top-level messaging array.
                if (entry.TryGetProperty("messaging", out var messaging))
                    await ProcessMessagesAsync(profile, account, messaging, result, cancellationToken);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Facebook webhook processing failed for {Id}", webhookEvent.Id);
            throw;
        }
    }

    /// <summary>
    /// Matches the delivery to a connected page. Meta's "Send to My Server" test tool always sends
    /// <c>entry.id = "0"</c>, so those fall back to a connected page instead of being dropped.
    /// </summary>
    private async Task<SocialProfile?> ResolveProfileAsync(
        string pageId,
        WebhookProcessResult result,
        CancellationToken cancellationToken)
    {
        var profile = await _unitOfWork.SocialProfiles.GetByExternalProfileIdAsync(pageId, cancellationToken);
        if (profile is not null)
            return profile;

        if (IsTestDeliveryId(pageId))
        {
            var pages = await _unitOfWork.SocialProfiles.FindAsync(
                p => p.ProfileType == ProfileType.FacebookPage, cancellationToken);
            profile = pages.OrderByDescending(p => p.CreatedAt).FirstOrDefault();

            if (profile is not null)
            {
                result.Skip($"Test delivery (entry id '{pageId}') applied to connected page '{profile.Name}'.");
                return profile;
            }
        }

        _logger.LogInformation("Facebook webhook ignored — page {PageId} is not connected.", pageId);
        result.Skip($"No connected Facebook page matches entry id '{pageId}'.");
        return null;
    }

    /// <summary>Meta's webhook test tool sends placeholder ids rather than a real page id.</summary>
    private static bool IsTestDeliveryId(string? id)
        => string.IsNullOrWhiteSpace(id) || id == "0";

    private async Task ProcessChangesAsync(
        SocialProfile profile,
        SocialAccount account,
        JsonElement entry,
        JsonElement changes,
        WebhookProcessResult result,
        CancellationToken cancellationToken)
    {
        foreach (var change in changes.EnumerateArray())
        {
            var field = change.TryGetProperty("field", out var fieldElement) ? fieldElement.GetString() : null;
            if (!change.TryGetProperty("value", out var value))
            {
                result.Skip($"Change '{field}' has no value object.");
                continue;
            }

            // Messaging can also arrive as a change with field=messages instead of entry.messaging.
            if (field is "messages" or "messaging_postbacks")
            {
                await ProcessMessageAsync(profile, account, value, result, cancellationToken);
                continue;
            }

            if (field != "feed")
            {
                result.Skip($"Field '{field}' is not handled.");
                continue;
            }

            var item = value.TryGetProperty("item", out var itemElement) ? itemElement.GetString() : null;
            var verb = value.TryGetProperty("verb", out var verbElement) ? verbElement.GetString() : "add";

            switch (item)
            {
                case "comment":
                    await ProcessCommentAsync(profile, account, entry, value, verb, result, cancellationToken);
                    break;
                case "reaction":
                    await ProcessReactionAsync(profile, value, verb, result, cancellationToken);
                    break;
                case "status" or "photo" or "video" or "share" or "link" or "post":
                    await UpsertPostAsync(profile, account, value, result, cancellationToken);
                    break;
                default:
                    result.Skip($"Feed item '{item}' is not handled.");
                    break;
            }
        }
    }

    private async Task ProcessCommentAsync(
        SocialProfile profile,
        SocialAccount account,
        JsonElement entry,
        JsonElement value,
        string? verb,
        WebhookProcessResult result,
        CancellationToken cancellationToken)
    {
        var commentId = value.TryGetProperty("comment_id", out var commentIdElement) ? commentIdElement.ToString() : null;
        if (string.IsNullOrWhiteSpace(commentId))
        {
            result.Skip("Comment change has no comment_id.");
            return;
        }

        var existing = await _unitOfWork.Comments.GetByExternalCommentIdAsync(commentId, cancellationToken);
        var message = value.TryGetProperty("message", out var messageElement) ? messageElement.GetString() ?? string.Empty : string.Empty;

        if (verb == "remove")
        {
            if (existing is null)
            {
                result.Skip($"Comment '{commentId}' was removed but is not stored.");
                return;
            }
            existing.IsDeleted = true;
            existing.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Comments.Update(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            result.Handled++;
            return;
        }

        if (existing is not null)
        {
            // "edited" replays the same comment id with new text.
            if (existing.Message == message)
            {
                result.Skip($"Comment '{commentId}' already stored.");
                return;
            }
            existing.Message = message;
            existing.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Comments.Update(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            result.Handled++;
            return;
        }

        var postExternalId = value.TryGetProperty("post_id", out var postIdElement) ? postIdElement.ToString() : null;
        if (string.IsNullOrWhiteSpace(postExternalId))
        {
            result.Skip($"Comment '{commentId}' has no post_id.");
            return;
        }

        var receivedAt = UnixSeconds(value, "created_time") ?? UnixSeconds(entry, "time") ?? DateTime.UtcNow;
        var post = await ResolvePostAsync(profile, account, postExternalId!, receivedAt, cancellationToken: cancellationToken);

        var authorId = string.Empty;
        var authorName = "Facebook user";
        if (value.TryGetProperty("from", out var from))
        {
            authorId = from.TryGetProperty("id", out var fromId) ? fromId.ToString() : string.Empty;
            authorName = from.TryGetProperty("name", out var fromName) ? fromName.GetString() ?? authorName : authorName;
        }

        // parent_id equals post_id for a top-level comment; only a real reply has a parent comment.
        Comment? parentComment = null;
        if (value.TryGetProperty("parent_id", out var parentIdElement))
        {
            var parentId = parentIdElement.ToString();
            if (!string.IsNullOrWhiteSpace(parentId) && parentId != postExternalId)
                parentComment = await _unitOfWork.Comments.GetByExternalCommentIdAsync(parentId, cancellationToken);
        }

        var comment = new Comment
        {
            PostId = post.Id,
            ParentCommentId = parentComment?.Id,
            ExternalCommentId = commentId!,
            AuthorId = authorId,
            AuthorName = authorName,
            Message = message,
            PlatformCreatedAt = receivedAt
        };
        await _unitOfWork.Comments.AddAsync(comment, cancellationToken);

        post.CommentCount += 1;
        post.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Posts.Update(post);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        result.Handled++;

        await _inboxRealtime.NotifyInboxItemAsync(account.UserId, new InboxItemDto
        {
            Id = comment.Id,
            ItemKind = "comment",
            PlatformCode = "facebook",
            ExternalId = comment.ExternalCommentId,
            AuthorName = comment.AuthorName,
            AuthorId = comment.AuthorId,
            Content = comment.Message,
            IsHidden = false,
            IsRead = false,
            IsOutgoing = !string.IsNullOrWhiteSpace(authorId) && authorId == profile.ExternalProfileId,
            ReceivedAt = receivedAt,
            CommentLikes = 0,
            ReplyCount = 0,
            Post = new InboxPostMetaDto
            {
                PostId = post.ExternalPostId ?? post.Id.ToString(),
                PageName = profile.Name ?? profile.Username ?? "Facebook",
                PostText = FirstNonEmpty(post.Text, post.Caption),
                PostImageUrl = post.MediaItems.FirstOrDefault()?.Url,
                LikesCount = post.LikeCount,
                CommentsCount = post.CommentCount,
                SharesCount = post.ShareCount,
                PostedAt = post.PublishedAt ?? post.CreatedAt
            }
        }, cancellationToken);
    }

    private async Task ProcessReactionAsync(
        SocialProfile profile,
        JsonElement value,
        string? verb,
        WebhookProcessResult result,
        CancellationToken cancellationToken)
    {
        var postExternalId = value.TryGetProperty("post_id", out var postIdElement) ? postIdElement.ToString() : null;
        if (string.IsNullOrWhiteSpace(postExternalId))
        {
            result.Skip("Reaction change has no post_id.");
            return;
        }

        var post = await _unitOfWork.Posts.GetByExternalPostIdAsync(profile.Id, postExternalId!, cancellationToken);
        if (post is null)
        {
            result.Skip($"Reaction ignored — post '{postExternalId}' is not stored.");
            return;
        }

        post.LikeCount = verb == "remove" ? Math.Max(0, post.LikeCount - 1) : post.LikeCount + 1;
        post.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Posts.Update(post);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        result.Handled++;
    }

    private async Task UpsertPostAsync(
        SocialProfile profile,
        SocialAccount account,
        JsonElement value,
        WebhookProcessResult result,
        CancellationToken cancellationToken)
    {
        var postExternalId = value.TryGetProperty("post_id", out var postIdElement) ? postIdElement.ToString() : null;
        if (string.IsNullOrWhiteSpace(postExternalId))
        {
            result.Skip("Feed post change has no post_id.");
            return;
        }

        var message = value.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : null;
        var publishedAt = UnixSeconds(value, "created_time") ?? DateTime.UtcNow;

        var post = await ResolvePostAsync(profile, account, postExternalId!, publishedAt, message, cancellationToken: cancellationToken);

        // The payload is authoritative for feed updates, since edits arrive as a new change.
        if (!string.IsNullOrWhiteSpace(message) && post.Text != message)
        {
            post.Text = message;
            post.Caption = message;
        }

        post.Type = ResolvePostType(value);
        post.Status = ContentPostStatus.Published;
        post.PublishedAt ??= publishedAt;
        post.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Posts.Update(post);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        result.Handled++;
    }

    /// <summary>
    /// Comments can arrive before the post is stored, so the post is read from Graph and saved
    /// first. Placeholder ids from the webhook test tool keep a readable stub instead.
    /// </summary>
    private async Task<Post> ResolvePostAsync(
        SocialProfile profile,
        SocialAccount account,
        string postExternalId,
        DateTime publishedAt,
        string? knownText = null,
        CancellationToken cancellationToken = default)
    {
        var pageToken = await ResolvePageTokenAsync(account, cancellationToken);

        return await MetaPostStore.ResolveAsync(
            _unitOfWork,
            profile,
            account.PlatformId,
            postExternalId,
            publishedAt,
            ct => GetPostSnapshotAsync(pageToken ?? string.Empty, postExternalId, ct),
            string.IsNullOrWhiteSpace(knownText) ? $"Facebook post {postExternalId}" : knownText!,
            cancellationToken);
    }

    private async Task<string?> ResolvePageTokenAsync(SocialAccount account, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(account.Auth?.AccessToken))
            return account.Auth!.AccessToken;

        var auth = await _unitOfWork.SocialAuths.GetBySocialAccountIdAsync(account.Id, cancellationToken);
        return string.IsNullOrWhiteSpace(auth?.AccessToken) ? null : auth!.AccessToken;
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private async Task ProcessMessagesAsync(
        SocialProfile profile,
        SocialAccount account,
        JsonElement messaging,
        WebhookProcessResult result,
        CancellationToken cancellationToken)
    {
        foreach (var item in messaging.EnumerateArray())
            await ProcessMessageAsync(profile, account, item, result, cancellationToken);
    }

    /// <summary>
    /// Handles one messaging item. Accepts both delivery shapes: an element of
    /// <c>entry.messaging[]</c> and the <c>changes[field=messages].value</c> object.
    /// </summary>
    private async Task ProcessMessageAsync(
        SocialProfile profile,
        SocialAccount account,
        JsonElement item,
        WebhookProcessResult result,
        CancellationToken cancellationToken)
    {
        if (!item.TryGetProperty("message", out var message))
        {
            result.Skip("Messaging item has no message object.");
            return;
        }

        var messageId = message.TryGetProperty("mid", out var mid) ? mid.ToString() : null;
        if (string.IsNullOrWhiteSpace(messageId))
        {
            result.Skip("Message has no mid.");
            return;
        }
        if (await _unitOfWork.Messages.GetByExternalMessageIdAsync(messageId, cancellationToken) is not null)
        {
            result.Skip($"Message '{messageId}' already stored.");
            return;
        }

        var senderId = item.TryGetProperty("sender", out var sender) && sender.TryGetProperty("id", out var senderValue)
            ? senderValue.ToString()
            : string.Empty;
        var receiverId = item.TryGetProperty("recipient", out var recipient) && recipient.TryGetProperty("id", out var recipientValue)
            ? recipientValue.ToString()
            : string.Empty;

        // Echoes are the page's own replies coming back through the webhook.
        var isEcho = message.TryGetProperty("is_echo", out var echo) && echo.ValueKind == JsonValueKind.True;
        var outbound = isEcho || senderId == profile.ExternalProfileId;
        var customerId = outbound ? receiverId : senderId;
        if (string.IsNullOrWhiteSpace(customerId))
        {
            result.Skip($"Message '{messageId}' has no sender/recipient id.");
            return;
        }

        var conversationKey = $"{profile.ExternalProfileId}:{customerId}";
        var conversation = await _unitOfWork.Conversations.GetByExternalConversationIdAsync(
            profile.Id, conversationKey, cancellationToken);
        var isNewConversation = conversation is null;
        if (conversation is null)
        {
            conversation = new Conversation
            {
                SocialProfileId = profile.Id,
                ExternalConversationId = conversationKey,
                CustomerId = customerId,
                CustomerName = customerId,
                Status = ConversationStatus.Open
            };
            await _unitOfWork.Conversations.AddAsync(conversation, cancellationToken);
        }

        var receivedAt = ReadTimestamp(item) ?? DateTime.UtcNow;
        var body = message.TryGetProperty("text", out var text)
            ? text.GetString()
            : message.TryGetProperty("attachments", out _) ? "[Facebook attachment]" : string.Empty;

        var row = new Message
        {
            ConversationId = conversation.Id,
            ExternalMessageId = messageId!,
            SenderId = senderId,
            ReceiverId = receiverId,
            Direction = outbound ? MessageDirection.Outbound : MessageDirection.Inbound,
            MessageType = MessageContentType.Text,
            Body = body,
            Status = outbound ? MessageDeliveryStatus.Sent : MessageDeliveryStatus.Delivered,
            PlatformCreatedAt = receivedAt
        };
        await _unitOfWork.Messages.AddAsync(row, cancellationToken);

        conversation.LastMessageAt = receivedAt;
        conversation.UpdatedAt = DateTime.UtcNow;
        if (!outbound) conversation.UnreadCount += 1;
        if (!isNewConversation) _unitOfWork.Conversations.Update(conversation);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        result.Handled++;

        await _inboxRealtime.NotifyInboxItemAsync(account.UserId, new InboxItemDto
        {
            Id = row.Id,
            ItemKind = "message",
            PlatformCode = "facebook",
            ExternalId = row.ExternalMessageId,
            AuthorName = outbound ? "You" : conversation.CustomerName ?? senderId,
            AuthorId = senderId,
            Content = body ?? string.Empty,
            IsHidden = false,
            IsRead = outbound,
            IsOutgoing = outbound,
            ConversationId = conversation.Id,
            ReceivedAt = receivedAt
        }, cancellationToken);
    }

    private static ContentPostType ResolvePostType(JsonElement value) =>
        value.TryGetProperty("item", out var item) ? item.GetString() switch
        {
            "photo" => ContentPostType.Image,
            "video" => ContentPostType.Video,
            _ => ContentPostType.Text
        } : ContentPostType.Text;

    private static DateTime? UnixSeconds(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : null;

    private static DateTime? UnixMilliseconds(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.TryGetInt64(out var milliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime
            : null;

    /// <summary>
    /// Reads a messaging timestamp. Meta sends it as a number or a string, and in seconds or
    /// milliseconds depending on the product, so both are normalised here.
    /// </summary>
    private static DateTime? ReadTimestamp(JsonElement element)
    {
        if (!element.TryGetProperty("timestamp", out var value))
            return null;

        long raw;
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (!value.TryGetInt64(out raw)) return null;
        }
        else if (value.ValueKind != JsonValueKind.String || !long.TryParse(value.GetString(), out raw))
        {
            return null;
        }

        if (raw <= 0) return null;

        return raw > 100_000_000_000L
            ? DateTimeOffset.FromUnixTimeMilliseconds(raw).UtcDateTime
            : DateTimeOffset.FromUnixTimeSeconds(raw).UtcDateTime;
    }

    private static List<PostDto> ParsePosts(JsonDocument doc)
    {
        var results = new List<PostDto>();
        if (!doc.RootElement.TryGetProperty("data", out var data)) return results;
        foreach (var item in data.EnumerateArray())
        {
            results.Add(new PostDto
            {
                Id = item.GetProperty("id").GetString() ?? string.Empty,
                Message = item.TryGetProperty("message", out var m) ? m.GetString() : null,
                Permalink = item.TryGetProperty("permalink_url", out var p) ? p.GetString() : null,
                CreatedTime = item.TryGetProperty("created_time", out var c) && DateTime.TryParse(c.GetString(), out var dt) ? dt : null
            });
        }
        return results;
    }

    private static List<CommentDto> ParseComments(JsonDocument doc)
    {
        var results = new List<CommentDto>();
        if (!doc.RootElement.TryGetProperty("data", out var data)) return results;
        foreach (var item in data.EnumerateArray())
        {
            results.Add(new CommentDto
            {
                Id = item.GetProperty("id").GetString() ?? string.Empty,
                Message = item.TryGetProperty("message", out var m) ? m.GetString() ?? string.Empty : string.Empty,
                FromId = item.TryGetProperty("from", out var from) && from.TryGetProperty("id", out var id) ? id.GetString() : null,
                FromName = item.TryGetProperty("from", out var from2) && from2.TryGetProperty("name", out var name) ? name.GetString() : null,
                CreatedTime = item.TryGetProperty("created_time", out var c) && DateTime.TryParse(c.GetString(), out var dt) ? dt : null,
                IsHidden = item.TryGetProperty("is_hidden", out var h) && h.GetBoolean()
            });
        }
        return results;
    }
}
