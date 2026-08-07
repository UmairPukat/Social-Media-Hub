namespace SocialMedia.Api.Configuration;

/// <summary>
/// Maps flat Railway environment variables into the nested configuration
/// sections used by <c>JwtSettings</c> and <c>MetaSettings</c>.
/// Nested keys (e.g. MetaSettings__Instagram__AppId) still take precedence
/// when already present.
/// </summary>
public static class RailwayEnvConfiguration
{
    public static IConfigurationBuilder AddRailwayFlatEnv(this IConfigurationBuilder builder)
    {
        var mapped = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        Map(mapped, "JwtSecretKey", "JwtSettings:SecretKey");
        Map(mapped, "JwtIssuer", "JwtSettings:Issuer");
        Map(mapped, "JwtAudience", "JwtSettings:Audience");
        Map(mapped, "JwtExpirationMinutes", "JwtSettings:ExpirationMinutes");

        MapPlatform(mapped, "facebook", "Facebook");
        MapPlatform(mapped, "instagram", "Instagram");
        MapPlatform(mapped, "whatsapp", "WhatsApp");

        Map(mapped, "whatsappPhoneNumberId", "MetaSettings:WhatsApp:PhoneNumberId");
        Map(mapped, "whatsappWabaId", "MetaSettings:WhatsApp:WabaId");

        if (mapped.Count == 0)
            return builder;

        return builder.AddInMemoryCollection(mapped);
    }

    private static void MapPlatform(IDictionary<string, string?> mapped, string prefix, string section)
    {
        Map(mapped, $"{prefix}AppId", $"MetaSettings:{section}:AppId");
        Map(mapped, $"{prefix}AppSecret", $"MetaSettings:{section}:AppSecret");
        Map(mapped, $"{prefix}RedirectUri", $"MetaSettings:{section}:RedirectUri");
        Map(mapped, $"{prefix}GraphApiVersion", $"MetaSettings:{section}:GraphApiVersion");
        Map(mapped, $"{prefix}WebhookVerifyToken", $"MetaSettings:{section}:WebhookVerifyToken");
    }

    private static void Map(IDictionary<string, string?> mapped, string envName, string configKey)
    {
        var value = Environment.GetEnvironmentVariable(envName);
        if (string.IsNullOrWhiteSpace(value))
            return;

        // Do not override an explicit nested env var already set on Railway.
        var nestedEnv = configKey.Replace(":", "__");
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(nestedEnv)))
            return;

        mapped[configKey] = value.Trim();
    }
}
