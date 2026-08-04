using SocialMedia.Domain.Common;

namespace SocialMedia.Domain.Entities;

/// <summary>
/// OAuth tokens for a SocialAccount. Never return tokens to the client.
/// </summary>
public class SocialAuth : BaseEntity
{
    public Guid SocialAccountId { get; set; }

    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Scopes { get; set; }
    public string? WebhookSecret { get; set; }

    public SocialAccount? SocialAccount { get; set; }
}
