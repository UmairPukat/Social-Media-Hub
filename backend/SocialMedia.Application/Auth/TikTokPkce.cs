using System.Security.Cryptography;
using System.Text;

namespace SocialMedia.Application.Auth;

/// <summary>
/// PKCE helpers for TikTok Login Kit (S256 code challenge).
/// </summary>
public static class TikTokPkce
{
    private const string PkceChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";

    public static string GenerateCodeVerifier(int length = 64)
    {
        var len = Math.Clamp(length, 43, 128);
        var bytes = RandomNumberGenerator.GetBytes(len);
        var builder = new StringBuilder(len);
        for (var i = 0; i < len; i++)
            builder.Append(PkceChars[bytes[i] % PkceChars.Length]);
        return builder.ToString();
    }

    /// <summary>TikTok expects hex(SHA256(verifier)) for code_challenge with method S256.</summary>
    public static string CodeChallengeFromVerifier(string verifier)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
