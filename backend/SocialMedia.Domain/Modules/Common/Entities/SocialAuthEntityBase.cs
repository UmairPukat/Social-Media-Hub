using SocialMedia.Domain.Common;

namespace SocialMedia.Domain.Modules.Common.Entities;

public abstract class SocialAuthEntityBase : BaseEntity
{
    public Guid SocialAccountId { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Scopes { get; set; }
    public string? WebhookSecret { get; set; }
}
