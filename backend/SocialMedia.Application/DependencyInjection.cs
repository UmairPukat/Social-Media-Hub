using Microsoft.Extensions.DependencyInjection;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Modules.AppConnections.Interfaces;
using SocialMedia.Application.Modules.AppConnections.Services;
using SocialMedia.Application.Modules.Common;
using SocialMedia.Application.Modules.DeveloperApps.Interfaces;
using SocialMedia.Application.Modules.DeveloperApps.Services;
using SocialMedia.Application.Modules.Integrations.Interfaces;
using SocialMedia.Application.Modules.Integrations.Services;
using SocialMedia.Application.Services;

namespace SocialMedia.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IIntegrationService, IntegrationService>();
        services.AddScoped<IProcessAppConfigService, ProcessAppConfigService>();
        services.AddScoped<IIntegrationsAppConfigService, IntegrationsAppConfigService>();
        services.AddScoped<IAppConnectionsAppConfigService, AppConnectionsAppConfigService>();
        services.AddScoped<IDeveloperAppsAppConfigService, DeveloperAppsAppConfigService>();
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<IInboxService, InboxService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddScoped<IYouTubeSyncService, YouTubeSyncService>();
        services.AddScoped<ITikTokSyncService, TikTokSyncService>();
        return services;
    }
}
