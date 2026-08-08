namespace SocialMedia.Application.DTOs.Meta;

/// <summary>
/// The URL the frontend should redirect the user to in order to start a Meta OAuth flow.
/// </summary>
public class MetaAuthUrlResponse
{
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Sent after the user is redirected back from Meta with an authorization code,
/// so the backend can exchange it for an access token.
/// </summary>
public class MetaTokenExchangeRequest
{
    public string Code { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
}

/// <summary>
/// Query values Meta sends when verifying a webhook subscription
/// (GET request with hub.mode / hub.challenge / hub.verify_token).
/// </summary>
public class MetaWebhookChallenge
{
    public string Mode { get; set; } = string.Empty;
    public string Challenge { get; set; } = string.Empty;
    public string VerifyToken { get; set; } = string.Empty;
}

/// <summary>
/// A comment as returned by a platform's Graph API call.
/// </summary>
public class CommentDto
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? FromId { get; set; }
    public string? FromName { get; set; }
    public DateTime? CreatedTime { get; set; }
    public bool IsHidden { get; set; }
}

/// <summary>
/// A direct message as returned by a platform's API call.
/// </summary>
public class MessageDto
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? FromId { get; set; }
    public string? FromName { get; set; }
    public DateTime? Timestamp { get; set; }
}

/// <summary>
/// A post as returned by a platform's API call.
/// </summary>
public class PostDto
{
    public string Id { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Permalink { get; set; }
    public DateTime? CreatedTime { get; set; }
}

/// <summary>
/// A single post/media read back from Graph so a webhook comment can be shown with the
/// same post context Meta displays: text, image, and engagement counts.
/// </summary>
public class RemotePostSnapshot
{
    public string ExternalId { get; set; } = string.Empty;
    public string? Text { get; set; }
    public string? Permalink { get; set; }
    public string? MediaUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public bool IsVideo { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public int ShareCount { get; set; }
    public DateTime? CreatedTime { get; set; }
}
