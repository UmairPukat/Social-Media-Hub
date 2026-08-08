using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SocialMedia.Application.Auth;

/// <summary>
/// Signed OAuth state so Meta can redirect to the backend without a JWT cookie.
/// Payload carries the signed-in user and platform for the shared Callback URL.
/// </summary>
public static class MetaOAuthState
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Create(Guid userId, string platformCode, string signingKey, TimeSpan? lifetime = null)
    {
        var payload = new Payload
        {
            UserId = userId,
            Platform = platformCode.Trim().ToLowerInvariant(),
            Nonce = Guid.NewGuid().ToString("N"),
            ExpiresAtUnix = DateTimeOffset.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(15)).ToUnixTimeSeconds()
        };

        var body = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions)));
        var sig = Base64UrlEncode(Sign(body, signingKey));
        return $"{body}.{sig}";
    }

    public static bool TryValidate(string? state, string signingKey, out Guid userId, out string platformCode, out string error)
    {
        userId = Guid.Empty;
        platformCode = string.Empty;
        error = "Invalid OAuth state.";

        if (string.IsNullOrWhiteSpace(state))
            return false;

        var parts = state.Split('.', 2);
        if (parts.Length != 2)
            return false;

        var body = parts[0];
        var suppliedSig = parts[1];
        var expectedSig = Base64UrlEncode(Sign(body, signingKey));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(suppliedSig),
                Encoding.UTF8.GetBytes(expectedSig)))
        {
            error = "OAuth state signature mismatch.";
            return false;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Base64UrlDecode(body));
            var payload = JsonSerializer.Deserialize<Payload>(json, JsonOptions);
            if (payload is null || payload.UserId == Guid.Empty || string.IsNullOrWhiteSpace(payload.Platform))
                return false;

            if (payload.ExpiresAtUnix < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                error = "OAuth state expired. Try connecting again.";
                return false;
            }

            if (payload.Platform is not ("facebook" or "instagram" or "whatsapp"))
            {
                error = $"Unsupported platform '{payload.Platform}'.";
                return false;
            }

            userId = payload.UserId;
            platformCode = payload.Platform;
            error = string.Empty;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] Sign(string body, string signingKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }

    private sealed class Payload
    {
        public Guid UserId { get; set; }
        public string Platform { get; set; } = string.Empty;
        public string Nonce { get; set; } = string.Empty;
        public long ExpiresAtUnix { get; set; }
    }
}
