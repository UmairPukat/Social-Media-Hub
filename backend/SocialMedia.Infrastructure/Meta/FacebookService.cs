using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly ILogger<FacebookService> _logger;

    public FacebookService(MetaGraphClient graph, IOptions<MetaSettings> options, IUnitOfWork unitOfWork, ILogger<FacebookService> logger)
    {
        _graph = graph;
        _settings = options.Value.Facebook;
        _unitOfWork = unitOfWork;
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

    public async Task ProcessWebhookPayloadAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        // Persist structured rows from payload into Comment / Message tables when possible.
        try
        {
            using var doc = JsonDocument.Parse(webhookEvent.PayloadJson);
            if (!doc.RootElement.TryGetProperty("entry", out var entries))
                return;

            foreach (var entry in entries.EnumerateArray())
            {
                var pageId = entry.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (pageId is null) continue;

                var profile = await _unitOfWork.SocialProfiles.GetByExternalProfileIdAsync(pageId, cancellationToken);
                if (profile is null) continue;

                if (!entry.TryGetProperty("changes", out var changes))
                    continue;

                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var value)) continue;
                    var commentId = value.TryGetProperty("comment_id", out var cid) ? cid.GetString() : null;
                    if (string.IsNullOrWhiteSpace(commentId)) continue;

                    // Find or create a placeholder post for parent post_id
                    var postExternalId = value.TryGetProperty("post_id", out var pid) ? pid.GetString() : pageId;
                    var posts = await _unitOfWork.Posts.FindAsync(p => p.ExternalPostId == postExternalId, cancellationToken);
                    var post = posts.FirstOrDefault();
                    if (post is null)
                    {
                        post = new Post
                        {
                            SocialProfileId = profile.Id,
                            PlatformId = profile.SocialAccount?.PlatformId ?? Guid.Empty,
                            ExternalPostId = postExternalId,
                            Status = ContentPostStatus.Published,
                            Text = string.Empty
                        };
                        // PlatformId fallback from account
                        if (post.PlatformId == Guid.Empty && profile.SocialAccountId != Guid.Empty)
                        {
                            var account = await _unitOfWork.SocialAccounts.GetByIdAsync(profile.SocialAccountId, cancellationToken);
                            post.PlatformId = account?.PlatformId ?? Guid.Empty;
                        }
                        await _unitOfWork.Posts.AddAsync(post, cancellationToken);
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                    }

                    await _unitOfWork.Comments.AddAsync(new Comment
                    {
                        PostId = post.Id,
                        ExternalCommentId = commentId,
                        AuthorName = value.TryGetProperty("from", out var from) && from.TryGetProperty("name", out var name)
                            ? name.GetString() ?? "Unknown" : "Unknown",
                        AuthorId = value.TryGetProperty("from", out var from2) && from2.TryGetProperty("id", out var fid)
                            ? fid.GetString() : null,
                        Message = value.TryGetProperty("message", out var msg) ? msg.GetString() ?? string.Empty : string.Empty,
                        PlatformCreatedAt = DateTime.UtcNow
                    }, cancellationToken);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Facebook webhook processing failed for {Id}", webhookEvent.Id);
            throw;
        }
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
