using System.ComponentModel.DataAnnotations;

namespace SocialMedia.Application.DTOs.Auth;

public class UserListItemDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminResetPasswordRequest
{
    [Required, MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}
