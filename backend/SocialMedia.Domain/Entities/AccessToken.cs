using SocialMedia.Domain.Common;

namespace SocialMedia.Domain.Entities;

/// <summary>
/// Invite token required for in-app signup.
/// </summary>
public class AccessToken : BaseEntity
{
    public string Token { get; set; } = string.Empty;
    public string? Label { get; set; }
    public bool IsUsed { get; set; }
    public Guid? UsedByUserId { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
