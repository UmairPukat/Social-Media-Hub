namespace SocialMedia.Application.Catalog;

/// <summary>
/// Three isolated connection processes. Each has its own API prefix, OAuth callback,
/// webhook endpoint, DB config table, and frontend workspace routes.
/// </summary>
public static class ProcessModules
{
    public static class Integrations
    {
        public const string MenuType = MenuTypes.Integration;
        public const string ApiRoute = "api/integrations";
        public const string CallbackRoute = "/api/integrations/callback";
        public const string WebhookRoute = "/api/integrations/webhooks";
        public const string WhatsAppCallbackRoute = "/api/integrations/whatsapp/callback";
        public const string WhatsAppWebhookRoute = "/api/integrations/whatsapp/webhooks";
        public const string FrontendBase = "/app/integrations";
        public const string Label = "Integrations";
    }

    public static class AppConnections
    {
        public const string MenuType = MenuTypes.AppConnection;
        public const string ApiRoute = "api/app-connections";
        public const string CallbackRoute = "/api/app-connections/callback";
        public const string WebhookRoute = "/api/app-connections/webhooks";
        public const string WhatsAppCallbackRoute = "/api/app-connections/whatsapp/callback";
        public const string WhatsAppWebhookRoute = "/api/app-connections/whatsapp/webhooks";
        public const string FrontendBase = "/app/app-connections";
        public const string Label = "App Connections";
    }

    public static class DeveloperApps
    {
        public const string MenuType = MenuTypes.DeveloperApp;
        public const string ApiRoute = "api/developer-apps";
        public const string CallbackRoute = "/api/developer-apps/callback";
        public const string WebhookRoute = "/api/developer-apps/webhooks";
        public const string WhatsAppCallbackRoute = "/api/developer-apps/whatsapp/callback";
        public const string WhatsAppWebhookRoute = "/api/developer-apps/whatsapp/webhooks";
        public const string FrontendBase = "/app/developer-apps";
        public const string Label = "Developer Apps";
    }

    public static IReadOnlyList<string> AllMenuTypes { get; } =
        [Integrations.MenuType, AppConnections.MenuType, DeveloperApps.MenuType];

    public static string? ApiRouteFor(string? menuType) =>
        MenuTypes.Normalize(menuType) switch
        {
            MenuTypes.Integration => Integrations.ApiRoute,
            MenuTypes.AppConnection => AppConnections.ApiRoute,
            MenuTypes.DeveloperApp => DeveloperApps.ApiRoute,
            _ => null
        };

    public static string CallbackRouteFor(string? menuType) =>
        MenuTypes.Normalize(menuType) switch
        {
            MenuTypes.AppConnection => AppConnections.CallbackRoute,
            MenuTypes.DeveloperApp => DeveloperApps.CallbackRoute,
            _ => Integrations.CallbackRoute
        };

    public static string WhatsAppCallbackRouteFor(string? menuType) =>
        MenuTypes.Normalize(menuType) switch
        {
            MenuTypes.AppConnection => AppConnections.WhatsAppCallbackRoute,
            MenuTypes.DeveloperApp => DeveloperApps.WhatsAppCallbackRoute,
            _ => Integrations.WhatsAppCallbackRoute
        };

    public static string WhatsAppWebhookRouteFor(string? menuType) =>
        MenuTypes.Normalize(menuType) switch
        {
            MenuTypes.AppConnection => AppConnections.WhatsAppWebhookRoute,
            MenuTypes.DeveloperApp => DeveloperApps.WhatsAppWebhookRoute,
            _ => Integrations.WhatsAppWebhookRoute
        };
}
