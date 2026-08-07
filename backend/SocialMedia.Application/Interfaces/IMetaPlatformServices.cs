using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Domain.Entities;

namespace SocialMedia.Application.Interfaces;

/// <summary>
/// Lightweight token + profile context for Graph API calls.
/// Auth URLs are built on the frontend; backend only uses stored tokens.
/// </summary>
public class MetaCallContext
{
    public string AccessToken { get; set; } = string.Empty;
    public string ProfileExternalId { get; set; } = string.Empty;
    /// <summary>Facebook Page ID used for IG Messaging when connected via Facebook Login.</summary>
    public string? PageExternalId { get; set; }
}

public interface IFacebookService
{
    /// <summary>Exchanges Meta authorization code for a user access token (server-side).</summary>
    Task<OAuthTokenResult> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default);

    Task<(string Id, string Name)> GetMeAsync(string accessToken, CancellationToken cancellationToken = default);

    Task<PostDto> CreatePostAsync(MetaCallContext context, string content, string? mediaUrl, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PostDto>> GetPostsAsync(MetaCallContext context, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CommentDto>> GetCommentsAsync(MetaCallContext context, string postId, CancellationToken cancellationToken = default);
    Task ReplyCommentAsync(MetaCallContext context, string commentId, string message, CancellationToken cancellationToken = default);
    Task HideCommentAsync(MetaCallContext context, string commentId, bool hide, CancellationToken cancellationToken = default);
    Task DeleteCommentAsync(MetaCallContext context, string commentId, CancellationToken cancellationToken = default);
    Task SendMessageAsync(MetaCallContext context, string recipientId, string message, CancellationToken cancellationToken = default);
    Task DeleteMessageAsync(MetaCallContext context, string messageId, CancellationToken cancellationToken = default);
    Task ProcessWebhookPayloadAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SocialProfileDraft>> DiscoverProfilesAsync(string userAccessToken, CancellationToken cancellationToken = default);
}

public interface IInstagramService
{
    /// <summary>Exchanges code using Facebook App credentials (Facebook Login for Instagram).</summary>
    Task<OAuthTokenResult> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default);

    Task<(string Id, string Name)> GetMeAsync(string accessToken, CancellationToken cancellationToken = default);

    Task<PostDto> CreatePostAsync(MetaCallContext context, string content, string? mediaUrl, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PostDto>> GetPostsAsync(MetaCallContext context, CancellationToken cancellationToken = default);
    Task ReplyCommentAsync(MetaCallContext context, string commentId, string message, CancellationToken cancellationToken = default);
    Task HideCommentAsync(MetaCallContext context, string commentId, bool hide, CancellationToken cancellationToken = default);
    Task DeleteCommentAsync(MetaCallContext context, string commentId, CancellationToken cancellationToken = default);
    Task SendMessageAsync(MetaCallContext context, string recipientId, string message, CancellationToken cancellationToken = default);
    Task DeleteMessageAsync(MetaCallContext context, string messageId, CancellationToken cancellationToken = default);
    Task SubscribeWebhooksAsync(string accessToken, CancellationToken cancellationToken = default);
    Task ProcessWebhookPayloadAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SocialProfileDraft>> DiscoverProfilesAsync(string userAccessToken, CancellationToken cancellationToken = default);
}

public interface IWhatsAppService
{
    Task<OAuthTokenResult> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default);

    Task<(string Id, string Name)> GetMeAsync(string accessToken, CancellationToken cancellationToken = default);

    Task SendMessageAsync(MetaCallContext context, string recipientId, string message, CancellationToken cancellationToken = default);
    Task DeleteMessageAsync(MetaCallContext context, string messageId, CancellationToken cancellationToken = default);
    Task ProcessWebhookPayloadAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SocialProfileDraft>> DiscoverProfilesAsync(string userAccessToken, string? phoneNumberId, string? wabaId, CancellationToken cancellationToken = default);
}

/// <summary>Result of exchanging a Meta OAuth authorization code.</summary>
public class OAuthTokenResult
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public string? TokenType { get; set; }
}

/// <summary>Temporary profile shape returned while connecting an account.</summary>
public class SocialProfileDraft
{
    public string ExternalProfileId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? ProfileImage { get; set; }
    public string ProfileType { get; set; } = string.Empty;
    public string? PageAccessToken { get; set; }
    /// <summary>Linked Facebook Page ID (Instagram via Facebook Login).</summary>
    public string? PageId { get; set; }
}
