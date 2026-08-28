using Microsoft.EntityFrameworkCore;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.AppConnections.Entities;
using SocialMedia.Domain.Modules.DeveloperApps.Entities;
using SocialMedia.Domain.Modules.Integrations.Entities;

namespace SocialMedia.Infrastructure.Persistence;

/// <summary>
/// SocialIntegration database context.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<AccessToken> AccessTokens => Set<AccessToken>();

    // Integrations module tables
    public DbSet<IntegrationPlatform> IntegrationPlatforms => Set<IntegrationPlatform>();
    public DbSet<IntegrationSocialAccount> IntegrationSocialAccounts => Set<IntegrationSocialAccount>();
    public DbSet<IntegrationSocialAuth> IntegrationSocialAuths => Set<IntegrationSocialAuth>();
    public DbSet<IntegrationSocialProfile> IntegrationSocialProfiles => Set<IntegrationSocialProfile>();
    public DbSet<IntegrationPost> IntegrationPosts => Set<IntegrationPost>();
    public DbSet<IntegrationMedia> IntegrationMedia => Set<IntegrationMedia>();
    public DbSet<IntegrationComment> IntegrationComments => Set<IntegrationComment>();
    public DbSet<IntegrationConversation> IntegrationConversations => Set<IntegrationConversation>();
    public DbSet<IntegrationMessage> IntegrationMessages => Set<IntegrationMessage>();
    public DbSet<IntegrationMessageAttachment> IntegrationMessageAttachments => Set<IntegrationMessageAttachment>();
    public DbSet<IntegrationWebhookEvent> IntegrationWebhookEvents => Set<IntegrationWebhookEvent>();
    public DbSet<IntegrationWebhookLog> IntegrationWebhookLogs => Set<IntegrationWebhookLog>();
    public DbSet<IntegrationSyncJob> IntegrationSyncJobs => Set<IntegrationSyncJob>();

    // App Connections module tables
    public DbSet<AppConnectionPlatform> AppConnectionPlatforms => Set<AppConnectionPlatform>();
    public DbSet<AppConnectionSocialAccount> AppConnectionSocialAccounts => Set<AppConnectionSocialAccount>();
    public DbSet<AppConnectionSocialAuth> AppConnectionSocialAuths => Set<AppConnectionSocialAuth>();
    public DbSet<AppConnectionSocialProfile> AppConnectionSocialProfiles => Set<AppConnectionSocialProfile>();
    public DbSet<AppConnectionPost> AppConnectionPosts => Set<AppConnectionPost>();
    public DbSet<AppConnectionMedia> AppConnectionMedia => Set<AppConnectionMedia>();
    public DbSet<AppConnectionComment> AppConnectionComments => Set<AppConnectionComment>();
    public DbSet<AppConnectionConversation> AppConnectionConversations => Set<AppConnectionConversation>();
    public DbSet<AppConnectionMessage> AppConnectionMessages => Set<AppConnectionMessage>();
    public DbSet<AppConnectionMessageAttachment> AppConnectionMessageAttachments => Set<AppConnectionMessageAttachment>();
    public DbSet<AppConnectionWebhookEvent> AppConnectionWebhookEvents => Set<AppConnectionWebhookEvent>();
    public DbSet<AppConnectionWebhookLog> AppConnectionWebhookLogs => Set<AppConnectionWebhookLog>();
    public DbSet<AppConnectionSyncJob> AppConnectionSyncJobs => Set<AppConnectionSyncJob>();

    // Developer Apps module tables
    public DbSet<DeveloperAppPlatform> DeveloperAppPlatforms => Set<DeveloperAppPlatform>();
    public DbSet<DeveloperAppSocialAccount> DeveloperAppSocialAccounts => Set<DeveloperAppSocialAccount>();
    public DbSet<DeveloperAppSocialAuth> DeveloperAppSocialAuths => Set<DeveloperAppSocialAuth>();
    public DbSet<DeveloperAppSocialProfile> DeveloperAppSocialProfiles => Set<DeveloperAppSocialProfile>();
    public DbSet<DeveloperAppPost> DeveloperAppPosts => Set<DeveloperAppPost>();
    public DbSet<DeveloperAppMedia> DeveloperAppMedia => Set<DeveloperAppMedia>();
    public DbSet<DeveloperAppComment> DeveloperAppComments => Set<DeveloperAppComment>();
    public DbSet<DeveloperAppConversation> DeveloperAppConversations => Set<DeveloperAppConversation>();
    public DbSet<DeveloperAppMessage> DeveloperAppMessages => Set<DeveloperAppMessage>();
    public DbSet<DeveloperAppMessageAttachment> DeveloperAppMessageAttachments => Set<DeveloperAppMessageAttachment>();
    public DbSet<DeveloperAppWebhookEvent> DeveloperAppWebhookEvents => Set<DeveloperAppWebhookEvent>();
    public DbSet<DeveloperAppWebhookLog> DeveloperAppWebhookLogs => Set<DeveloperAppWebhookLog>();
    public DbSet<DeveloperAppSyncJob> DeveloperAppSyncJobs => Set<DeveloperAppSyncJob>();

    // Config tables
    public DbSet<AppConnectionConfig> AppConnectionConfigs => Set<AppConnectionConfig>();
    public DbSet<IntegrationAppConfig> IntegrationAppConfigs => Set<IntegrationAppConfig>();
    public DbSet<DeveloperAppConfig> DeveloperAppConfigs => Set<DeveloperAppConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
