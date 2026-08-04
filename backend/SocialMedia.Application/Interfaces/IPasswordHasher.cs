namespace SocialMedia.Application.Interfaces;

/// <summary>
/// Hashes and verifies passwords. Kept as an interface so the Application layer
/// never depends directly on a specific hashing library (BCrypt lives in Infrastructure).
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);

    bool VerifyPassword(string password, string passwordHash);
}
