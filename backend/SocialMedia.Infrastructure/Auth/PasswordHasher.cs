using SocialMedia.Application.Interfaces;

namespace SocialMedia.Infrastructure.Auth;

/// <summary>
/// Thin wrapper around BCrypt so Application never references the hashing library directly.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
        => BCrypt.Net.BCrypt.HashPassword(password);

    public bool VerifyPassword(string password, string passwordHash)
        => BCrypt.Net.BCrypt.Verify(password, passwordHash);
}
