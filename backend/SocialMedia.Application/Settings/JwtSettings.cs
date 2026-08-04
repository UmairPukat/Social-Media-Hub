namespace SocialMedia.Application.Settings;

/// <summary>
/// JWT signing configuration, bound from the "JwtSettings" section of appsettings.
/// The actual token is generated in Infrastructure via IJwtTokenService.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// How long an issued token stays valid.
    /// </summary>
    public int ExpirationMinutes { get; set; } = 60;
}
