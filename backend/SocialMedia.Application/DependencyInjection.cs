using Microsoft.Extensions.DependencyInjection;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Services;

namespace SocialMedia.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IIntegrationService, IntegrationService>();
        services.AddScoped<IAppConnectionService, AppConnectionService>();
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<IInboxService, InboxService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IWebhookService, WebhookService>();
        return services;
    }
}
