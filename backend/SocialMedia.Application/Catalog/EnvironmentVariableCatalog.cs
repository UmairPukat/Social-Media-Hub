using SocialMedia.Application.DTOs.EnvironmentVariables;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Application.Catalog;

/// <summary>
/// Default deployment environment variables seeded on first run.
/// Values are placeholders — real secrets belong in the hosting provider, not source control.
/// </summary>
public static class EnvironmentVariableCatalog
{
    public sealed record Definition(
        string Name,
        string Description,
        bool IsRequired,
        EnvironmentVariableScope Scope,
        string DefaultValue = "");

    public static readonly IReadOnlyList<Definition> All =
    [
        // Frontend (Railway / CI build)
        new("API_URL", "Backend API base URL used by the Angular app at build time.", true, EnvironmentVariableScope.Frontend, "http://localhost:5080/api"),
        new("HUB_URL", "SignalR hub URL for real-time inbox updates.", true, EnvironmentVariableScope.Frontend, "http://localhost:5080/hubs/inbox"),
        new("META_FACEBOOK_APP_ID", "Facebook app id shown in integration UI hints.", true, EnvironmentVariableScope.Frontend),
        new("META_INSTAGRAM_APP_ID", "Instagram app id shown in integration UI hints.", true, EnvironmentVariableScope.Frontend),
        new("META_INSTAGRAM_LOGIN_APP_ID", "Instagram Login app id for standalone IG OAuth.", false, EnvironmentVariableScope.Frontend),
        new("META_WHATSAPP_APP_ID", "WhatsApp app id shown in integration UI hints.", false, EnvironmentVariableScope.Frontend),

        // Backend (Railway / hosting)
        new("JwtSecretKey", "JWT signing key — must be long and unique in production.", true, EnvironmentVariableScope.Backend),
        new("JwtIssuer", "JWT token issuer claim.", true, EnvironmentVariableScope.Backend, "SocialMediaHub"),
        new("JwtAudience", "JWT token audience claim.", true, EnvironmentVariableScope.Backend, "SocialMediaHubClients"),
        new("JwtExpirationMinutes", "JWT access token lifetime in minutes.", false, EnvironmentVariableScope.Backend, "720"),
        new("DATABASE_URL", "Postgres connection URL (Railway provides this automatically).", true, EnvironmentVariableScope.Backend),
        new("corsOrigins", "Comma-separated allowed frontend origins for CORS.", true, EnvironmentVariableScope.Backend, "http://localhost:4200"),
        new("SEED_ON_STARTUP", "When true, seeds platforms and default admin on startup.", false, EnvironmentVariableScope.Backend, "true"),
        new("facebookAppId", "Facebook Meta app id (flat Railway env alias).", true, EnvironmentVariableScope.Backend),
        new("facebookAppSecret", "Facebook Meta app secret.", true, EnvironmentVariableScope.Backend),
        new("instagramAppId", "Instagram Meta app id (flat Railway env alias).", true, EnvironmentVariableScope.Backend),
        new("instagramAppSecret", "Instagram Meta app secret.", true, EnvironmentVariableScope.Backend),
        new("instagramLoginAppId", "Instagram Login app id for graph.instagram.com OAuth.", false, EnvironmentVariableScope.Backend),
        new("instagramLoginAppSecret", "Instagram Login app secret.", false, EnvironmentVariableScope.Backend),
        new("whatsappAppId", "WhatsApp Business app id.", false, EnvironmentVariableScope.Backend),
        new("whatsappAppSecret", "WhatsApp Business app secret.", false, EnvironmentVariableScope.Backend),
        new("metaRedirectUri", "Shared OAuth callback URL for all Meta products.", true, EnvironmentVariableScope.Backend, "http://localhost:5080/api/Integrations/Callback"),
        new("whatsappPhoneNumberId", "WhatsApp Cloud API phone number id.", false, EnvironmentVariableScope.Backend),
        new("whatsappWabaId", "WhatsApp Business Account id.", false, EnvironmentVariableScope.Backend),
        new("META_FACEBOOK_APP_SECRET", "Facebook app secret (META_* alias accepted by API).", true, EnvironmentVariableScope.Backend),
        new("META_INSTAGRAM_APP_SECRET", "Instagram app secret (META_* alias).", true, EnvironmentVariableScope.Backend),
        new("META_INSTAGRAM_LOGIN_APP_SECRET", "Instagram Login app secret (META_* alias).", false, EnvironmentVariableScope.Backend),
        new("META_WHATSAPP_APP_SECRET", "WhatsApp app secret (META_* alias).", false, EnvironmentVariableScope.Backend)
    ];
}
