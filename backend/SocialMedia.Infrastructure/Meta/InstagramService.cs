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

    public async Task<RemotePostSnapshot?> GetMediaSnapshotAsync(string accessToken, string mediaId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(mediaId))
            return null;

        using var doc = await _graph.GetAsync(GraphVersion, mediaId, accessToken, cancellationToken,
            ("fields", "id,caption,media_type,media_url,thumbnail_url,permalink,timestamp,like_count,comments_count,children{id,media_type,media_url,thumbnail_url}"));

        var root = doc.RootElement;
        var mediaType = root.TryGetProperty("media_type", out var type) ? type.GetString() : null;
        var mediaUrl = root.TryGetProperty("media_url", out var rootMediaUrl)
            ? rootMediaUrl.GetString()
            : null;
        var thumbnailUrl = root.TryGetProperty("thumbnail_url", out var rootThumbnail)
            ? rootThumbnail.GetString()
            : null;

        // Carousel albums may not expose a usable URL on the parent. Use the first child
        // as the inbox preview so the attachment is still persisted and displayed.
        if (string.IsNullOrWhiteSpace(mediaUrl) &&
            string.IsNullOrWhiteSpace(thumbnailUrl) &&
            root.TryGetProperty("children", out var children) &&
            children.TryGetProperty("data", out var childData) &&
            childData.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in childData.EnumerateArray())
            {
                var childMedia = child.TryGetProperty("media_url", out var childMediaUrl)
                    ? childMediaUrl.GetString()
                    : null;
                var childThumb = child.TryGetProperty("thumbnail_url", out var childThumbnail)
                    ? childThumbnail.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(childMedia) && string.IsNullOrWhiteSpace(childThumb))
                    continue;

                mediaType = child.TryGetProperty("media_type", out var childType)
                    ? childType.GetString()
                    : mediaType;
                mediaUrl = childMedia;
                thumbnailUrl = childThumb;
                break;
            }
        }

        return new RemotePostSnapshot
        {
            ExternalId = root.TryGetProperty("id", out var id) ? id.GetString() ?? mediaId : mediaId,
            Text = root.TryGetProperty("caption", out var caption) ? caption.GetString() : null,
            Permalink = root.TryGetProperty("permalink", out var permalink) ? permalink.GetString() : null,
            MediaUrl = mediaUrl,
            ThumbnailUrl = thumbnailUrl,
            IsVideo = string.Equals(mediaType, "VIDEO", StringComparison.OrdinalIgnoreCase),
            LikeCount = root.TryGetProperty("like_count", out var likes) && likes.TryGetInt32(out var likeCount) ? likeCount : 0,
            CommentCount = root.TryGetProperty("comments_count", out var comments) && comments.TryGetInt32(out var commentCount) ? commentCount : 0,
            CreatedTime = root.TryGetProperty("timestamp", out var timestamp) &&
                          DateTime.TryParse(timestamp.GetString(), out var createdAt)
                ? createdAt.ToUniversalTime()
                : null
        };
    }

    private async Task<RemotePostSnapshot?> GetMediaSnapshotWithTokensAsync(
        IReadOnlyList<string> accessTokens,
        string mediaId,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        foreach (var accessToken in accessTokens)
        {
            try
            {
                return await GetMediaSnapshotAsync(accessToken, mediaId, cancellationToken);
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        if (lastError is not null)
            throw lastError;
        return null;
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

    public async Task<RemoteCommentSnapshot?> GetCommentSnapshotAsync(string accessToken, string commentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(commentId))
            return null;

        using var doc = await _graph.GetAsync(GraphVersion, commentId, accessToken, cancellationToken,
            ("fields", "id,text,timestamp,username,from,like_count,parent_id,media"));

        var root = doc.RootElement;
        string? authorId = null;
        string? authorName = root.TryGetProperty("username", out var username) ? username.GetString() : null;
        if (root.TryGetProperty("from", out var from))
        {
            authorId = from.TryGetProperty("id", out var fromId) ? fromId.ToString() : null;
            if (from.TryGetProperty("username", out var fromUser) && !string.IsNullOrWhiteSpace(fromUser.GetString()))
                authorName = fromUser.GetString();
        }

        return new RemoteCommentSnapshot
        {
            ExternalId = root.TryGetProperty("id", out var id) ? id.GetString() ?? commentId : commentId,
            Message = root.TryGetProperty("text", out var text) ? text.GetString() : null,
            PostExternalId = root.TryGetProperty("media", out var media) && media.TryGetProperty("id", out var mediaId)
                ? mediaId.ToString()
                : null,
            ParentExternalId = root.TryGetProperty("parent_id", out var parentId) ? parentId.ToString() : null,
            AuthorId = authorId,
            AuthorName = authorName,
            AuthorUsername = authorName,
            LikeCount = root.TryGetProperty("like_count", out var likes) && likes.TryGetInt32(out var likeCount) ? likeCount : 0,
            CreatedTime = root.TryGetProperty("timestamp", out var timestamp) &&
                          DateTime.TryParse(timestamp.GetString(), out var createdAt)
                ? createdAt.ToUniversalTime()
                : null
        };
    }

    public async Task<string?> ReplyCommentAsync(MetaCallContext context, string commentId, string message, CancellationToken cancellationToken = default)
    {
        using var doc = await _graph.PostAsync(GraphVersion, $"{commentId}/replies", context.AccessToken,
            new Dictionary<string, string> { ["message"] = message }, cancellationToken);
        return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
    }

    public async Task HideCommentAsync(MetaCallContext context, string commentId, bool hide, CancellationToken cancellationToken = default)
    {
        using var _ = await _graph.PostAsync(GraphVersion, commentId, context.AccessToken,
            new Dictionary<string, string> { ["hide"] = hide ? "true" : "false" }, cancellationToken);
    }

    public Task DeleteCommentAsync(MetaCallContext context, string commentId, CancellationToken cancellationToken = default)
        => _graph.DeleteAsync(GraphVersion, commentId, context.AccessToken, cancellationToken);

    public async Task<string?> SendMessageAsync(MetaCallContext context, string recipientId, string message, string? replyToMid = null, CancellationToken cancellationToken = default)
    {
        // Facebook Login for Instagram Messaging uses the Page ID + Page access token.
        var pathId = !string.IsNullOrWhiteSpace(context.PageExternalId)
            ? context.PageExternalId
            : context.ProfileExternalId;
        object payload = string.IsNullOrWhiteSpace(replyToMid)
            ? new
            {
                recipient = new { id = recipientId },
                messaging_type = "RESPONSE",
                message = new { text = message }
            }
            : new
            {
                recipient = new { id = recipientId },
                messaging_type = "RESPONSE",
                message = new { text = message },
                reply_to = new { mid = replyToMid }
            };
        using var doc = await _graph.PostJsonAsync(GraphVersion, $"{pathId}/messages", context.AccessToken, payload, cancellationToken);
        if (doc.RootElement.TryGetProperty("message_id", out var messageId))
            return messageId.GetString();
        return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
    }

    public Task DeleteMessageAsync(MetaCallContext context, string messageId, CancellationToken cancellationToken = default)
        => _graph.DeleteAsync(GraphVersion, messageId, context.AccessToken, cancellationToken);

    /// <summary>Subscribe the linked Facebook Page to comment and message webhook fields.</summary>
    public Task SubscribePageWebhooksAsync(string pageId, string pageAccessToken, CancellationToken cancellationToken = default)
        => _graph.SubscribePageAsync(GraphVersion, pageId, pageAccessToken, MetaGraphClient.InstagramPageSubscribedFields, cancellationToken);

    public Task UnsubscribePageWebhooksAsync(string pageId, string pageAccessToken, CancellationToken cancellationToken = default)
        => _graph.UnsubscribePageAsync(GraphVersion, pageId, pageAccessToken, cancellationToken);

    public Task<IReadOnlyList<string>> GetSubscribedFieldsAsync(string pageId, string pageAccessToken, CancellationToken cancellationToken = default)
        => _graph.GetPageSubscribedFieldsAsync(GraphVersion, pageId, pageAccessToken, cancellationToken);

    public async Task<WebhookProcessResult> ProcessWebhookPayloadAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        var result = new WebhookProcessResult();
        try
        {
            using var doc = JsonDocument.Parse(webhookEvent.PayloadJson);
            if (!doc.RootElement.TryGetProperty("entry", out var entries))
            {
                result.Skip("Payload has no 'entry' array — not a Meta webhook delivery.");
                return result;
            }

            foreach (var entry in entries.EnumerateArray())
            {
                var igUserId = entry.TryGetProperty("id", out var idEl) ? idEl.ToString() : null;
                if (string.IsNullOrWhiteSpace(igUserId))
                {
                    result.Skip("Entry has no id.");
                    continue;
                }

                var profile = await ResolveProfileAsync(igUserId!, result, cancellationToken);
                if (profile is null)
                    continue;

                if (entry.TryGetProperty("changes", out var changes))
                    await ProcessChangesAsync(profile, entry, changes, result, cancellationToken);

                if (entry.TryGetProperty("messaging", out var messaging))
                    await ProcessMessagesAsync(profile, messaging, result, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Instagram webhook processing failed for {Id}", webhookEvent.Id);
            throw;
        }
    }

    /// <summary>
    /// Facebook Login webhooks key <c>entry.id</c> to the IG user or the linked Page. Meta's
    /// "Send to My Server" test tool sends <c>"0"</c>, so those fall back to a connected profile.
    /// </summary>
    private async Task<SocialProfile?> ResolveProfileAsync(
        string entryId,
        WebhookProcessResult result,
        CancellationToken cancellationToken)
    {
        var profile = await _unitOfWork.SocialProfiles.GetByExternalProfileIdAsync(entryId, cancellationToken)
            ?? await FindProfileByPageIdAsync(entryId, cancellationToken);
        if (profile is not null)
            return profile;

        if (string.IsNullOrWhiteSpace(entryId) || entryId == "0")
        {
            var profiles = await _unitOfWork.SocialProfiles.FindAsync(
                p => p.ProfileType == ProfileType.InstagramBusiness, cancellationToken);
            profile = profiles.OrderByDescending(p => p.CreatedAt).FirstOrDefault();

            if (profile is not null)
            {
                result.Skip($"Test delivery (entry id '{entryId}') applied to connected profile '{profile.Name}'.");
                return profile;
            }
        }

        _logger.LogInformation("Instagram webhook ignored — no profile matches entry {EntryId}.", entryId);
        result.Skip($"No connected Instagram profile matches entry id '{entryId}'.");
        return null;
    }

    private async Task<IReadOnlyList<string>> ResolveAccessTokensAsync(
        SocialAccount account,
        CancellationToken cancellationToken)
    {
        var auth = account.Auth
            ?? await _unitOfWork.SocialAuths.GetBySocialAccountIdAsync(account.Id, cancellationToken);
        var tokens = new List<string>();

        void Add(string? token)
        {
            if (!string.IsNullOrWhiteSpace(token) && !tokens.Contains(token))
                tokens.Add(token);
        }

        // The selected Page token is normally AccessToken; RefreshToken retains the
        // long-lived user token and is a useful fallback for Graph media reads.
        Add(auth?.AccessToken);
        Add(auth?.RefreshToken);
        return tokens;
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

    private async Task ProcessChangesAsync(
        SocialProfile profile,
        JsonElement entry,
        JsonElement changes,
        WebhookProcessResult result,
        CancellationToken cancellationToken)
    {
        var account = await _unitOfWork.SocialAccounts.GetByIdAsync(profile.SocialAccountId, cancellationToken);
        if (account is null)
        {
            result.Skip($"Profile '{profile.Id}' has no owning account.");
            return;
        }

        foreach (var change in changes.EnumerateArray())
        {
            var field = change.TryGetProperty("field", out var fieldElement) ? fieldElement.GetString() : null;
            if (!change.TryGetProperty("value", out var value))
            {
                result.Skip($"Change '{field}' has no value object.");
                continue;
            }

            // Instagram messaging can arrive as a change with field=messages rather than entry.messaging.
            if (field is "messages" or "messaging_postbacks" or "message_reactions")
            {
                await ProcessMessageAsync(profile, account, value, result, cancellationToken);
                continue;
            }

            if (field is not ("comments" or "live_comments"))
            {
                result.Skip($"Field '{field}' is not handled.");
                continue;
            }

            var commentId = value.TryGetProperty("id", out var commentIdElement)
                ? commentIdElement.ToString()
                : null;
            if (string.IsNullOrWhiteSpace(commentId))
            {
                result.Skip("Comment change is missing id.");
                continue;
            }

            var accessTokens = await ResolveAccessTokensAsync(account, cancellationToken);
            RemoteCommentSnapshot? enriched = null;
            foreach (var accessToken in accessTokens)
            {
                try
                {
                    enriched = await GetCommentSnapshotAsync(accessToken, commentId!, cancellationToken);
                    break;
                }
                catch (Exception ex)
                {
                    result.Skip($"Graph comment enrich failed for '{commentId}': {ex.Message}");
                }
            }

            var mediaId = FirstNonEmpty(
                enriched?.PostExternalId,
                value.TryGetProperty("media", out var media) && media.TryGetProperty("id", out var mediaIdElement)
                    ? mediaIdElement.ToString()
                    : null);
            if (string.IsNullOrWhiteSpace(mediaId))
            {
                result.Skip("Comment change is missing media.id.");
                continue;
            }

            var commentText = FirstNonEmpty(
                enriched?.Message,
                value.TryGetProperty("text", out var text) ? text.GetString() : null) ?? string.Empty;

            // Resolve the post before checking comment idempotency. A redelivered comment can
            // therefore repair an older post row that was saved without its Instagram media.
            var post = await MetaPostStore.ResolveAsync(
                _unitOfWork,
                profile,
                account.PlatformId,
                mediaId!,
                enriched?.CreatedTime ?? UnixSeconds(entry, "time") ?? DateTime.UtcNow,
                ct => GetMediaSnapshotWithTokensAsync(accessTokens, mediaId!, ct),
                "Instagram post",
                requireMedia: true,
                cancellationToken: cancellationToken);

            var existing = await _unitOfWork.Comments.GetByExternalCommentIdAsync(commentId, cancellationToken);
            if (existing is not null)
            {
                var changed = existing.Message != commentText || existing.PostId != post.Id;
                if (!changed)
                {
                    result.Skip($"Comment '{commentId}' already stored.");
                    continue;
                }

                existing.PostId = post.Id;
                existing.Message = commentText;
                if (enriched?.LikeCount > 0) existing.LikeCount = enriched.LikeCount;
                existing.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Comments.Update(existing);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                result.Handled++;
                continue;
            }

            var authorId = FirstNonEmpty(
                enriched?.AuthorId,
                value.TryGetProperty("from", out var from) && from.TryGetProperty("id", out var fromId)
                    ? fromId.ToString()
                    : null) ?? string.Empty;
            var authorName = FirstNonEmpty(
                enriched?.AuthorUsername,
                enriched?.AuthorName,
                value.TryGetProperty("from", out var fromUser) && fromUser.TryGetProperty("username", out var username)
                    ? username.GetString()
                    : null) ?? "Instagram user";

            Comment? parentComment = null;
            var parentExternalId = FirstNonEmpty(
                enriched?.ParentExternalId,
                value.TryGetProperty("parent_id", out var parentIdElement) ? parentIdElement.ToString() : null);
            if (!string.IsNullOrWhiteSpace(parentExternalId) && parentExternalId != mediaId)
                parentComment = await _unitOfWork.Comments.GetByExternalCommentIdAsync(parentExternalId!, cancellationToken);

            var receivedAt = enriched?.CreatedTime ?? UnixSeconds(entry, "time") ?? DateTime.UtcNow;
            var comment = new Comment
            {
                PostId = post.Id,
                ParentCommentId = parentComment?.Id,
                ExternalCommentId = commentId,
                AuthorId = authorId,
                AuthorName = authorName,
                Message = commentText,
                LikeCount = enriched?.LikeCount ?? 0,
                PlatformCreatedAt = receivedAt
            };
            await _unitOfWork.Comments.AddAsync(comment, cancellationToken);
            post.CommentCount += 1;
            post.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Posts.Update(post);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            result.Handled++;

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
                CommentLikes = comment.LikeCount,
                ReplyCount = 0,
                ParentId = comment.ParentCommentId,
                Post = new InboxPostMetaDto
                {
                    PostId = post.ExternalPostId ?? post.Id.ToString(),
                    PageName = profile.Name ?? profile.Username ?? "Instagram",
                    PostText = FirstNonEmpty(post.Caption, post.Text),
                    PostImageUrl = post.MediaItems.FirstOrDefault()?.Url,
                    LikesCount = post.LikeCount,
                    CommentsCount = post.CommentCount,
                    SharesCount = post.ShareCount,
                    PostedAt = post.PublishedAt ?? post.CreatedAt
                }
            };

            await _inboxRealtime.NotifyInboxItemAsync(account.UserId, inboxItem, cancellationToken);
        }
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private async Task ProcessMessagesAsync(
        SocialProfile profile,
        JsonElement messaging,
        WebhookProcessResult result,
        CancellationToken cancellationToken)
    {
        var account = await _unitOfWork.SocialAccounts.GetByIdAsync(profile.SocialAccountId, cancellationToken);
        if (account is null)
        {
            result.Skip($"Profile '{profile.Id}' has no owning account.");
            return;
        }

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
            : message.TryGetProperty("attachments", out _) ? "[Instagram attachment]" : string.Empty;

        var replyToMid = message.TryGetProperty("reply_to", out var replyTo) &&
                         replyTo.ValueKind == JsonValueKind.Object &&
                         replyTo.TryGetProperty("mid", out var quotedMid)
            ? quotedMid.ToString()
            : null;
        var quoted = string.IsNullOrWhiteSpace(replyToMid)
            ? null
            : await _unitOfWork.Messages.GetByExternalMessageIdAsync(replyToMid!, cancellationToken);

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
            PlatformCreatedAt = receivedAt,
            ReplyToMessageId = quoted?.Id,
            ReplyToExternalId = replyToMid
        };
        await _unitOfWork.Messages.AddAsync(msg, cancellationToken);

        conversation.LastMessageAt = receivedAt;
        conversation.UpdatedAt = DateTime.UtcNow;
        if (!outbound) conversation.UnreadCount += 1;
        if (!isNewConversation) _unitOfWork.Conversations.Update(conversation);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        result.Handled++;

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
            ReceivedAt = receivedAt,
            ReplyToId = quoted?.Id,
            ReplyToAuthor = quoted is null
                ? null
                : quoted.Direction == MessageDirection.Outbound ? "You" : conversation.CustomerName ?? quoted.SenderId,
            ReplyToContent = quoted?.Body
        };

        await _inboxRealtime.NotifyInboxItemAsync(account.UserId, inboxItem, cancellationToken);
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
}
