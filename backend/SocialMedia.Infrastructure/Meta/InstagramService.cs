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
/// Instagram Graph API. Auth uses Facebook Login on the frontend.
/// </summary>
public class InstagramService : IInstagramService
{
    private readonly MetaGraphClient _graph;
    private readonly InstagramSettings _instagram;
    private readonly FacebookSettings _facebook;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InstagramService> _logger;

    public InstagramService(MetaGraphClient graph, IOptions<MetaSettings> options, IUnitOfWork unitOfWork, ILogger<InstagramService> logger)
    {
        _graph = graph;
        _instagram = options.Value.Instagram;
        _facebook = options.Value.Facebook;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    private string GraphVersion =>
        string.IsNullOrWhiteSpace(_instagram.GraphApiVersion) ? _facebook.GraphApiVersion : _instagram.GraphApiVersion;

    /// <summary>Instagram Business uses Facebook Login — exchange with Facebook App Id/Secret.</summary>
    public async Task<OAuthTokenResult> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        using var shortLived = await _graph.GetAsync(
            GraphVersion, "oauth/access_token", string.Empty, cancellationToken,
            ("client_id", _facebook.AppId),
            ("client_secret", _facebook.AppSecret),
            ("redirect_uri", redirectUri),
            ("code", code));

        var shortToken = shortLived.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Meta did not return an access token.");

        try
        {
            using var longLived = await _graph.GetAsync(
                GraphVersion, "oauth/access_token", string.Empty, cancellationToken,
                ("grant_type", "fb_exchange_token"),
                ("client_id", _facebook.AppId),
                ("client_secret", _facebook.AppSecret),
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
        using var pagesDoc = await _graph.GetAsync(GraphVersion, "me/accounts", userAccessToken, cancellationToken,
            ("fields", "id,name,access_token,instagram_business_account{id,username,profile_picture_url}"));

        var list = new List<SocialProfileDraft>();
        if (!pagesDoc.RootElement.TryGetProperty("data", out var data))
            return list;

        foreach (var page in data.EnumerateArray())
        {
            if (!page.TryGetProperty("instagram_business_account", out var ig))
                continue;

            list.Add(new SocialProfileDraft
            {
                ExternalProfileId = ig.GetProperty("id").GetString() ?? string.Empty,
                Name = ig.TryGetProperty("username", out var u) ? u.GetString() ?? "Instagram" : "Instagram",
                Username = ig.TryGetProperty("username", out var u2) ? u2.GetString() : null,
                ProfileImage = ig.TryGetProperty("profile_picture_url", out var pic) ? pic.GetString() : null,
                ProfileType = "InstagramBusiness",
                PageAccessToken = page.TryGetProperty("access_token", out var t) ? t.GetString() : null
            });
        }

        return list;
    }

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
        var payload = new { recipient = new { id = recipientId }, message = new { text = message } };
        using var _ = await _graph.PostJsonAsync(GraphVersion, $"{context.ProfileExternalId}/messages", context.AccessToken, payload, cancellationToken);
    }

    public Task DeleteMessageAsync(MetaCallContext context, string messageId, CancellationToken cancellationToken = default)
        => _graph.DeleteAsync(GraphVersion, messageId, context.AccessToken, cancellationToken);

    public async Task ProcessWebhookPayloadAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(webhookEvent.PayloadJson);
            if (!doc.RootElement.TryGetProperty("entry", out var entries))
                return;

            foreach (var entry in entries.EnumerateArray())
            {
                var igUserId = entry.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (igUserId is null) continue;
                var profile = await _unitOfWork.SocialProfiles.GetByExternalProfileIdAsync(igUserId, cancellationToken);
                if (profile is null || !entry.TryGetProperty("changes", out var changes)) continue;

                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var value)) continue;
                    var externalId = value.TryGetProperty("id", out var vid) ? vid.GetString() : null;
                    if (string.IsNullOrWhiteSpace(externalId)) continue;

                    var account = await _unitOfWork.SocialAccounts.GetByIdAsync(profile.SocialAccountId, cancellationToken);
                    var post = new Post
                    {
                        SocialProfileId = profile.Id,
                        PlatformId = account?.PlatformId ?? Guid.Empty,
                        ExternalPostId = externalId,
                        Status = ContentPostStatus.Published,
                        Text = value.TryGetProperty("text", out var text) ? text.GetString() : string.Empty
                    };
                    await _unitOfWork.Posts.AddAsync(post, cancellationToken);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Instagram webhook processing failed for {Id}", webhookEvent.Id);
            throw;
        }
    }
}
