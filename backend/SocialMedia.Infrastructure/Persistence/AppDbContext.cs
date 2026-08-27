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
    public DbSet<Platform> Platforms => Set<Platform>();
    public DbSet<SocialAccount> SocialAccounts => Set<SocialAccount>();
    public DbSet<SocialAuth> SocialAuths => Set<SocialAuth>();
    public DbSet<SocialProfile> SocialProfiles => Set<SocialProfile>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Media> MediaItems => Set<Media>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<WebhookLog> WebhookLogs => Set<WebhookLog>();
    public DbSet<SyncJob> SyncJobs => Set<SyncJob>();
    public DbSet<AppConnectionConfig> AppConnectionConfigs => Set<AppConnectionConfig>();
    public DbSet<IntegrationAppConfig> IntegrationAppConfigs => Set<IntegrationAppConfig>();
    public DbSet<DeveloperAppConfig> DeveloperAppConfigs => Set<DeveloperAppConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
