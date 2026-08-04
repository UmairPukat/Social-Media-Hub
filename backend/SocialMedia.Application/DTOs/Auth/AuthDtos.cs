using System.ComponentModel.DataAnnotations;

namespace SocialMedia.Application.DTOs.Auth;

/// <summary>
/// Credentials submitted on the login form.
/// </summary>
public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Signup form data. Requires a valid, unused AccessToken to succeed.
/// </summary>
public class SignupRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// The gate-keeping token that unlocks registration.
    /// </summary>
    [Required]
    public string AccessToken { get; set; } = string.Empty;
}

/// <summary>
/// Returned after a successful login or signup.
/// </summary>
public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
