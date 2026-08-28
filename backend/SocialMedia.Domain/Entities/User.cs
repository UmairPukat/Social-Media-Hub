using SocialMedia.Domain.Common;

namespace SocialMedia.Domain.Entities;

/// <summary>
/// Application user (JWT login / invite signup). Not a Meta social account.
/// </summary>
public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
}
