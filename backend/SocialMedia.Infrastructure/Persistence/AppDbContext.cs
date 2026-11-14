using Microsoft.EntityFrameworkCore;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.AppConnections.Entities;
using SocialMedia.Domain.Modules.AccountConnections.Entities;
using SocialMedia.Domain.Modules.AccountManagement.Entities;
using SocialMedia.Domain.Modules.AccountHub.Entities;
using SocialMedia.Domain.Modules.DeveloperApps.Entities;
using SocialMedia.Domain.Modules.SocialMediaAccounts.Entities;
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


    // Social Media Accounts module tables
    public DbSet<SocialMediaAccountPlatform> SocialMediaAccountPlatforms => Set<SocialMediaAccountPlatform>();
    public DbSet<SocialMediaAccountSocialAccount> SocialMediaAccountSocialAccounts => Set<SocialMediaAccountSocialAccount>();
    public DbSet<SocialMediaAccountSocialAuth> SocialMediaAccountSocialAuths => Set<SocialMediaAccountSocialAuth>();
    public DbSet<SocialMediaAccountSocialProfile> SocialMediaAccountSocialProfiles => Set<SocialMediaAccountSocialProfile>();
    public DbSet<SocialMediaAccountPost> SocialMediaAccountPosts => Set<SocialMediaAccountPost>();
    public DbSet<SocialMediaAccountMedia> SocialMediaAccountMedia => Set<SocialMediaAccountMedia>();
    public DbSet<SocialMediaAccountComment> SocialMediaAccountComments => Set<SocialMediaAccountComment>();
    public DbSet<SocialMediaAccountConversation> SocialMediaAccountConversations => Set<SocialMediaAccountConversation>();
    public DbSet<SocialMediaAccountMessage> SocialMediaAccountMessages => Set<SocialMediaAccountMessage>();
    public DbSet<SocialMediaAccountMessageAttachment> SocialMediaAccountMessageAttachments => Set<SocialMediaAccountMessageAttachment>();
    public DbSet<SocialMediaAccountWebhookEvent> SocialMediaAccountWebhookEvents => Set<SocialMediaAccountWebhookEvent>();
    public DbSet<SocialMediaAccountWebhookLog> SocialMediaAccountWebhookLogs => Set<SocialMediaAccountWebhookLog>();
    public DbSet<SocialMediaAccountSyncJob> SocialMediaAccountSyncJobs => Set<SocialMediaAccountSyncJob>();

    // Account Connections module tables
    public DbSet<AccountConnectionPlatform> AccountConnectionPlatforms => Set<AccountConnectionPlatform>();
    // Account Management module tables
    public DbSet<AccountManagementPlatform> AccountManagementPlatforms => Set<AccountManagementPlatform>();
    // Account Hub module tables
    public DbSet<AccountHubPlatform> AccountHubPlatforms => Set<AccountHubPlatform>();
    public DbSet<AccountConnectionSocialAccount> AccountConnectionSocialAccounts => Set<AccountConnectionSocialAccount>();
    public DbSet<AccountManagementSocialAccount> AccountManagementSocialAccounts => Set<AccountManagementSocialAccount>();
    public DbSet<AccountHubSocialAccount> AccountHubSocialAccounts => Set<AccountHubSocialAccount>();
    public DbSet<AccountConnectionSocialAuth> AccountConnectionSocialAuths => Set<AccountConnectionSocialAuth>();
    public DbSet<AccountManagementSocialAuth> AccountManagementSocialAuths => Set<AccountManagementSocialAuth>();
    public DbSet<AccountHubSocialAuth> AccountHubSocialAuths => Set<AccountHubSocialAuth>();
    public DbSet<AccountConnectionSocialProfile> AccountConnectionSocialProfiles => Set<AccountConnectionSocialProfile>();
    public DbSet<AccountManagementSocialProfile> AccountManagementSocialProfiles => Set<AccountManagementSocialProfile>();
    public DbSet<AccountHubSocialProfile> AccountHubSocialProfiles => Set<AccountHubSocialProfile>();
    public DbSet<AccountConnectionPost> AccountConnectionPosts => Set<AccountConnectionPost>();
    public DbSet<AccountManagementPost> AccountManagementPosts => Set<AccountManagementPost>();
    public DbSet<AccountHubPost> AccountHubPosts => Set<AccountHubPost>();
    public DbSet<AccountConnectionMedia> AccountConnectionMedia => Set<AccountConnectionMedia>();
    public DbSet<AccountManagementMedia> AccountManagementMedia => Set<AccountManagementMedia>();
    public DbSet<AccountHubMedia> AccountHubMedia => Set<AccountHubMedia>();
    public DbSet<AccountConnectionComment> AccountConnectionComments => Set<AccountConnectionComment>();
    public DbSet<AccountManagementComment> AccountManagementComments => Set<AccountManagementComment>();
    public DbSet<AccountHubComment> AccountHubComments => Set<AccountHubComment>();
    public DbSet<AccountConnectionConversation> AccountConnectionConversations => Set<AccountConnectionConversation>();
    public DbSet<AccountManagementConversation> AccountManagementConversations => Set<AccountManagementConversation>();
    public DbSet<AccountHubConversation> AccountHubConversations => Set<AccountHubConversation>();
    public DbSet<AccountConnectionMessage> AccountConnectionMessages => Set<AccountConnectionMessage>();
    public DbSet<AccountManagementMessage> AccountManagementMessages => Set<AccountManagementMessage>();
    public DbSet<AccountHubMessage> AccountHubMessages => Set<AccountHubMessage>();
    public DbSet<AccountConnectionMessageAttachment> AccountConnectionMessageAttachments => Set<AccountConnectionMessageAttachment>();
    public DbSet<AccountManagementMessageAttachment> AccountManagementMessageAttachments => Set<AccountManagementMessageAttachment>();
    public DbSet<AccountHubMessageAttachment> AccountHubMessageAttachments => Set<AccountHubMessageAttachment>();
    public DbSet<AccountConnectionWebhookEvent> AccountConnectionWebhookEvents => Set<AccountConnectionWebhookEvent>();
    public DbSet<AccountManagementWebhookEvent> AccountManagementWebhookEvents => Set<AccountManagementWebhookEvent>();
    public DbSet<AccountHubWebhookEvent> AccountHubWebhookEvents => Set<AccountHubWebhookEvent>();
    public DbSet<AccountConnectionWebhookLog> AccountConnectionWebhookLogs => Set<AccountConnectionWebhookLog>();
    public DbSet<AccountManagementWebhookLog> AccountManagementWebhookLogs => Set<AccountManagementWebhookLog>();
    public DbSet<AccountHubWebhookLog> AccountHubWebhookLogs => Set<AccountHubWebhookLog>();
    public DbSet<AccountConnectionSyncJob> AccountConnectionSyncJobs => Set<AccountConnectionSyncJob>();
    public DbSet<AccountManagementSyncJob> AccountManagementSyncJobs => Set<AccountManagementSyncJob>();
    public DbSet<AccountHubSyncJob> AccountHubSyncJobs => Set<AccountHubSyncJob>();

    // Config tables
    public DbSet<AppConnectionConfig> AppConnectionConfigs => Set<AppConnectionConfig>();
    public DbSet<IntegrationAppConfig> IntegrationAppConfigs => Set<IntegrationAppConfig>();
    public DbSet<DeveloperAppConfig> DeveloperAppConfigs => Set<DeveloperAppConfig>();
    public DbSet<SocialMediaAccountConfig> SocialMediaAccountConfigs => Set<SocialMediaAccountConfig>();
    public DbSet<AccountConnectionConfig> AccountConnectionConfigs => Set<AccountConnectionConfig>();
    public DbSet<AccountManagementConfig> AccountManagementConfigs => Set<AccountManagementConfig>();
    public DbSet<AccountHubConfig> AccountHubConfigs => Set<AccountHubConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
