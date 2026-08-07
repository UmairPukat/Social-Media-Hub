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
/// Instagram Graph API via Facebook Login for Business.
/// OAuth uses Facebook App credentials; Graph calls use the Page access token.
/// </summary>
public class InstagramService : IInstagramService
{
    private readonly MetaGraphClient _graph;
    private readonly InstagramSettings _instagram;
    private readonly FacebookSettings _facebook;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInboxRealtimeNotifier _inboxRealtime;
    private readonly ILogger<InstagramService> _logger;

    public InstagramService(
        MetaGraphClient graph,
        IOptions<MetaSettings> options,
        IUnitOfWork unitOfWork,
        IInboxRealtimeNotifier inboxRealtime,
        ILogger<InstagramService> logger)
    {
        _graph = graph;
        _instagram = options.Value.Instagram;
        _facebook = options.Value.Facebook;
        _unitOfWork = unitOfWork;
        _inboxRealtime = inboxRealtime;
        _logger = logger;
    }

    private string GraphVersion =>
        !string.IsNullOrWhiteSpace(_instagram.GraphApiVersion)
            ? _instagram.GraphApiVersion
            : !string.IsNullOrWhiteSpace(_facebook.GraphApiVersion)
                ? _facebook.GraphApiVersion
                : "v21.0";

    private string AppId =>
        !string.IsNullOrWhiteSpace(_facebook.AppId) ? _facebook.AppId : _instagram.AppId;

    private string AppSecret =>
        !string.IsNullOrWhiteSpace(_facebook.AppSecret) ? _facebook.AppSecret : _instagram.AppSecret;

    /// <summary>Facebook Login: authorization code → short token → long-lived user token.</summary>
    public async Task<OAuthTokenResult> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(AppId) || string.IsNullOrWhiteSpace(AppSecret))
            throw new InvalidOperationException("Facebook AppId/AppSecret are required for Instagram Facebook Login.");

        using var shortLived = await _graph.GetAsync(
            GraphVersion, "oauth/access_token", string.Empty, cancellationToken,
            ("client_id", AppId),
            ("client_secret", AppSecret),
            ("redirect_uri", redirectUri),
            ("code", code));

        var shortToken = shortLived.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Meta did not return an access token.");

        try
        {
            using var longLived = await _graph.GetAsync(
                GraphVersion, "oauth/access_token", string.Empty, cancellationToken,
                ("grant_type", "fb_exchange_token"),
                ("client_id", AppId),
                ("client_secret", AppSecret),
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
        using var doc = await _graph.GetAsync(GraphVersion, "me", accessToken, cancellationToken, ("fields", "id,name"));
        var id = doc.RootElement.GetProperty("id").GetString() ?? string.Empty;
        var name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? "Instagram User" : "Instagram User";
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
            GraphVersion,
            "me/accounts",
            userAccessToken,
            cancellationToken,
            ("fields", "id,name,access_token,instagram_business_account{id,username,profile_picture_url,name}"));

        var list = new List<SocialProfileDraft>();
        if (!pagesDoc.RootElement.TryGetProperty("data", out var data))
            return list;

        foreach (var page in data.EnumerateArray())
        {
            if (!page.TryGetProperty("instagram_business_account", out var ig))
                continue;

            var username = ig.TryGetProperty("username", out var u) ? u.GetString() : null;
            var name = ig.TryGetProperty("name", out var n) ? n.GetString() : null;
            list.Add(new SocialProfileDraft
            {
                ExternalProfileId = ig.GetProperty("id").GetString() ?? string.Empty,
                Name = name ?? username ?? "Instagram",
                Username = username,
                ProfileImage = ig.TryGetProperty("profile_picture_url", out var pic) ? pic.GetString() : null,
                ProfileType = "InstagramBusiness",
                PageId = page.TryGetProperty("id", out var pageId) ? pageId.GetString() : null,
                PageAccessToken = page.TryGetProperty("access_token", out var t) ? t.GetString() : null
            });
        }

        return list;
    }

    public Task<IReadOnlyList<MetaPageInfo>> ListPagesAsync(string userAccessToken, CancellationToken cancellationToken = default)
        => _graph.ListPagesAsync(GraphVersion, userAccessToken, cancellationToken);

    public async Task<PostDto> CreatePostAsync(MetaCallContext context, string content, string? mediaUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl))
            throw new InvalidOperationException("Instagram posts require a publicly reachable MediaUrl.");

        using var containerDoc = await _graph.PostAsync(GraphVersion, $"{context.ProfileExternalId}/media", context.AccessToken,
            new Dictionary<string, string> { ["image_url"] = mediaUrl, ["caption"] = content }, cancellationToken);
        var creationId = containerDoc.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Instagram did not return a media container id.");

        using var publishDoc = await _graph.PostAsync(GraphVersion, $"{context.ProfileExternalId}/media_publish", context.AccessToken,
            new Dictionary<string, string> { ["creation_id"] = creationId }, cancellationToken);

        return new PostDto
        {
            Id = publishDoc.RootElement.GetProperty("id").GetString() ?? creationId,
            Message = content,
            CreatedTime = DateTime.UtcNow
        };
    }

    public async Task<IReadOnlyList<PostDto>> GetPostsAsync(MetaCallContext context, CancellationToken cancellationToken = default)
    {
        using var doc = await _graph.GetAsync(GraphVersion, $"{context.ProfileExternalId}/media", context.AccessToken, cancellationToken,
            ("fields", "id,caption,permalink,timestamp"), ("limit", "25"));

        var results = new List<PostDto>();
        if (!doc.RootElement.TryGetProperty("data", out var data)) return results;
        foreach (var item in data.EnumerateArray())
        {
            results.Add(new PostDto
            {
                Id = item.GetProperty("id").GetString() ?? string.Empty,
                Message = item.TryGetProperty("caption", out var c) ? c.GetString() : null,
                Permalink = item.TryGetProperty("permalink", out var p) ? p.GetString() : null,
                CreatedTime = item.TryGetProperty("timestamp", out var t) && DateTime.TryParse(t.GetString(), out var dt) ? dt : null
            });
        }
        return results;
    }

    public async Task ReplyCommentAsync(MetaCallContext context, string commentId, string message, CancellationToken cancellationToken = default)
    {
        using var _ = await _graph.PostAsync(GraphVersion, $"{commentId}/replies", context.AccessToken,
            new Dictionary<string, string> { ["message"] = message }, cancellationToken);
    }

    public async Task HideCommentAsync(MetaCallContext context, string commentId, bool hide, CancellationToken cancellationToken = default)
    {
        using var _ = await _graph.PostAsync(GraphVersion, commentId, context.AccessToken,
            new Dictionary<string, string> { ["hide"] = hide ? "true" : "false" }, cancellationToken);
    }

    public Task DeleteCommentAsync(MetaCallContext context, string commentId, CancellationToken cancellationToken = default)
        => _graph.DeleteAsync(GraphVersion, commentId, context.AccessToken, cancellationToken);

    public async Task SendMessageAsync(MetaCallContext context, string recipientId, string message, CancellationToken cancellationToken = default)
    {
        // Facebook Login for Instagram Messaging uses the Page ID + Page access token.
        var pathId = !string.IsNullOrWhiteSpace(context.PageExternalId)
            ? context.PageExternalId
            : context.ProfileExternalId;
        var payload = new
        {
            recipient = new { id = recipientId },
            messaging_type = "RESPONSE",
            message = new { text = message }
        };
        using var _ = await _graph.PostJsonAsync(GraphVersion, $"{pathId}/messages", context.AccessToken, payload, cancellationToken);
    }

    public Task DeleteMessageAsync(MetaCallContext context, string messageId, CancellationToken cancellationToken = default)
        => _graph.DeleteAsync(GraphVersion, messageId, context.AccessToken, cancellationToken);

    /// <summary>Subscribe the linked Facebook Page to Instagram webhook fields.</summary>
    public async Task SubscribeWebhooksAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var _ = await _graph.PostAsync(
            GraphVersion,
            "me/subscribed_apps",
            accessToken,
            new Dictionary<string, string>
            {
                ["subscribed_fields"] = "feed,messages,messaging_postbacks,messaging_seen,message_deliveries"
            },
            cancellationToken);
    }

    public async Task ProcessWebhookPayloadAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(webhookEvent.PayloadJson);
            if (!doc.RootElement.TryGetProperty("entry", out var entries))
                return;

            foreach (var entry in entries.EnumerateArray())
            {
                var igUserId = entry.TryGetProperty("id", out var idEl) ? idEl.ToString() : null;
                if (igUserId is null) continue;

                // Facebook Login webhooks may key entry.id to IG user or Page id.
                var profile = await _unitOfWork.SocialProfiles.GetByExternalProfileIdAsync(igUserId, cancellationToken)
                    ?? await FindProfileByPageIdAsync(igUserId, cancellationToken);
                if (profile is null) continue;

                if (entry.TryGetProperty("changes", out var changes))
                    await ProcessCommentChangesAsync(profile, entry, changes, cancellationToken);

                if (entry.TryGetProperty("messaging", out var messaging))
                    await ProcessMessagesAsync(profile, messaging, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Instagram webhook processing failed for {Id}", webhookEvent.Id);
            throw;
        }
    }

    private async Task<SocialProfile?> FindProfileByPageIdAsync(string pageId, CancellationToken cancellationToken)
    {
        var profiles = await _unitOfWork.SocialProfiles.FindAsync(
            p => p.ProfileType == ProfileType.InstagramBusiness && p.MetadataJson != null && p.MetadataJson.Contains(pageId),
            cancellationToken);
        return profiles.FirstOrDefault(p =>
        {
            try
            {
                using var meta = JsonDocument.Parse(p.MetadataJson!);
                return meta.RootElement.TryGetProperty("pageId", out var id) && id.GetString() == pageId;
            }
            catch
            {
                return false;
            }
        });
    }

    private async Task ProcessCommentChangesAsync(
        SocialProfile profile,
        JsonElement entry,
        JsonElement changes,
        CancellationToken cancellationToken)
    {
        var account = await _unitOfWork.SocialAccounts.GetByIdAsync(profile.SocialAccountId, cancellationToken);
        if (account is null) return;

        foreach (var change in changes.EnumerateArray())
        {
            var field = change.TryGetProperty("field", out var fieldElement) ? fieldElement.GetString() : null;
            if (field is not ("comments" or "live_comments") ||
                !change.TryGetProperty("value", out var value))
                continue;

            var commentId = value.TryGetProperty("id", out var commentIdElement)
                ? commentIdElement.ToString()
                : null;
            var mediaId = value.TryGetProperty("media", out var media) &&
                          media.TryGetProperty("id", out var mediaIdElement)
                ? mediaIdElement.ToString()
                : null;
            if (string.IsNullOrWhiteSpace(commentId) || string.IsNullOrWhiteSpace(mediaId))
                continue;
            if (await _unitOfWork.Comments.GetByExternalCommentIdAsync(commentId, cancellationToken) is not null)
                continue;

            // Attach to existing post by media/post id; create a stub post if missing.
            var post = await _unitOfWork.Posts.GetByExternalPostIdAsync(profile.Id, mediaId, cancellationToken);
            var isNewPost = post is null;
            if (post is null)
            {
                post = new Post
                {
                    SocialProfileId = profile.Id,
                    PlatformId = account.PlatformId,
                    ExternalPostId = mediaId,
                    Status = ContentPostStatus.Published,
                    PublishedAt = UnixSeconds(entry, "time"),
                    Text = string.Empty,
                    Caption = string.Empty
                };
                await _unitOfWork.Posts.AddAsync(post, cancellationToken);
            }

            var authorId = string.Empty;
            var authorName = "Instagram user";
            if (value.TryGetProperty("from", out var from))
            {
                authorId = from.TryGetProperty("id", out var fromId) ? fromId.ToString() : string.Empty;
                authorName = from.TryGetProperty("username", out var username)
                    ? username.GetString() ?? authorName
                    : authorName;
            }
            Comment? parentComment = null;
            if (value.TryGetProperty("parent_id", out var parentIdElement))
            {
                var parentId = parentIdElement.ToString();
                if (!string.IsNullOrWhiteSpace(parentId))
                    parentComment = await _unitOfWork.Comments.GetByExternalCommentIdAsync(parentId, cancellationToken);
            }

            var commentText = value.TryGetProperty("text", out var text) ? text.GetString() ?? string.Empty : string.Empty;
            var receivedAt = UnixSeconds(entry, "time") ?? DateTime.UtcNow;
            var comment = new Comment
            {
                PostId = post.Id,
                ParentCommentId = parentComment?.Id,
                ExternalCommentId = commentId,
                AuthorId = authorId,
                AuthorName = authorName,
                Message = commentText,
                PlatformCreatedAt = receivedAt
            };
            await _unitOfWork.Comments.AddAsync(comment, cancellationToken);
            post.CommentCount += 1;
            if (!isNewPost) _unitOfWork.Posts.Update(post);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var inboxItem = new InboxItemDto
            {
                Id = comment.Id,
                ItemKind = "comment",
                PlatformCode = "instagram",
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
                    PageName = profile.Name ?? profile.Username ?? "Instagram",
                    PostText = post.Caption ?? post.Text ?? string.Empty,
                    LikesCount = post.LikeCount,
                    CommentsCount = post.CommentCount,
                    SharesCount = post.ShareCount,
                    PostedAt = post.PublishedAt ?? post.CreatedAt
                }
            };

            await _inboxRealtime.NotifyInboxItemAsync(account.UserId, inboxItem, cancellationToken);
        }
    }

    private async Task ProcessMessagesAsync(
        SocialProfile profile,
        JsonElement messaging,
        CancellationToken cancellationToken)
    {
        var account = await _unitOfWork.SocialAccounts.GetByIdAsync(profile.SocialAccountId, cancellationToken);
        if (account is null) return;

        foreach (var item in messaging.EnumerateArray())
        {
            if (!item.TryGetProperty("message", out var message)) continue;
            var messageId = message.TryGetProperty("mid", out var mid) ? mid.GetString() : null;
            if (string.IsNullOrWhiteSpace(messageId) ||
                await _unitOfWork.Messages.GetByExternalMessageIdAsync(messageId, cancellationToken) is not null)
                continue;

            var senderId = item.TryGetProperty("sender", out var sender) &&
                           sender.TryGetProperty("id", out var senderValue)
                ? senderValue.ToString()
                : string.Empty;
            var receiverId = item.TryGetProperty("recipient", out var recipient) &&
                             recipient.TryGetProperty("id", out var recipientValue)
                ? recipientValue.ToString()
                : string.Empty;
            var isEcho = message.TryGetProperty("is_echo", out var echo) && echo.ValueKind == JsonValueKind.True;
            var pageId = TryReadPageId(profile.MetadataJson);
            var outbound = isEcho ||
                           senderId == profile.ExternalProfileId ||
                           (!string.IsNullOrWhiteSpace(pageId) && senderId == pageId);
            var customerId = outbound ? receiverId : senderId;
            if (string.IsNullOrWhiteSpace(customerId)) continue;

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

            var receivedAt = UnixMilliseconds(item, "timestamp") ?? DateTime.UtcNow;
            var body = message.TryGetProperty("text", out var text)
                ? text.GetString()
                : message.TryGetProperty("attachments", out _) ? "[Instagram attachment]" : string.Empty;

            var msg = new Message
            {
                ConversationId = conversation.Id,
                ExternalMessageId = messageId,
                SenderId = senderId,
                ReceiverId = receiverId,
                Direction = outbound ? MessageDirection.Outbound : MessageDirection.Inbound,
                MessageType = MessageContentType.Text,
                Body = body,
                Status = outbound ? MessageDeliveryStatus.Sent : MessageDeliveryStatus.Delivered,
                PlatformCreatedAt = receivedAt
            };
            await _unitOfWork.Messages.AddAsync(msg, cancellationToken);

            conversation.LastMessageAt = receivedAt;
            conversation.UpdatedAt = DateTime.UtcNow;
            if (!outbound) conversation.UnreadCount += 1;
            if (!isNewConversation) _unitOfWork.Conversations.Update(conversation);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var inboxItem = new InboxItemDto
            {
                Id = msg.Id,
                ItemKind = "message",
                PlatformCode = "instagram",
                ExternalId = msg.ExternalMessageId,
                AuthorName = outbound ? "You" : conversation.CustomerName ?? senderId,
                AuthorId = senderId,
                Content = body ?? string.Empty,
                IsHidden = false,
                IsRead = outbound,
                IsOutgoing = outbound,
                ConversationId = conversation.Id,
                ReceivedAt = receivedAt
            };

            await _inboxRealtime.NotifyInboxItemAsync(account.UserId, inboxItem, cancellationToken);
        }
    }

    private static string? TryReadPageId(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return null;
        try
        {
            using var meta = JsonDocument.Parse(metadataJson);
            return meta.RootElement.TryGetProperty("pageId", out var pageId) ? pageId.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static DateTime? UnixSeconds(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : null;

    private static DateTime? UnixMilliseconds(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.TryGetInt64(out var milliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime
            : null;
}
