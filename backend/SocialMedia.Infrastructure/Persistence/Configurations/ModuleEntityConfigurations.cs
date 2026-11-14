using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.Integrations.Entities;
using SocialMedia.Domain.Modules.AppConnections.Entities;
using SocialMedia.Domain.Modules.DeveloperApps.Entities;
using SocialMedia.Domain.Modules.SocialMediaAccounts.Entities;
using SocialMedia.Domain.Modules.AccountConnections.Entities;
using SocialMedia.Domain.Modules.AccountManagement.Entities;
using SocialMedia.Domain.Modules.AccountHub.Entities;

namespace SocialMedia.Infrastructure.Persistence.Configurations;

public class IntegrationPlatformConfiguration : IEntityTypeConfiguration<IntegrationPlatform>
{
    public void Configure(EntityTypeBuilder<IntegrationPlatform> builder)
    {
        builder.ToTable("IntegrationPlatforms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Icon).HasMaxLength(500);
    }
}

public class IntegrationSocialAccountConfiguration : IEntityTypeConfiguration<IntegrationSocialAccount>
{
    public void Configure(EntityTypeBuilder<IntegrationSocialAccount> builder)
    {
        builder.ToTable("IntegrationSocialAccounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalAccountId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Username).HasMaxLength(200);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.HasIndex(x => new { x.UserId, x.PlatformId, x.ExternalAccountId }).IsUnique();
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Platform).WithMany(x => x.SocialAccounts).HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Auth).WithOne(x => x.SocialAccount).HasForeignKey<IntegrationSocialAuth>(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class IntegrationSocialAuthConfiguration : IEntityTypeConfiguration<IntegrationSocialAuth>
{
    public void Configure(EntityTypeBuilder<IntegrationSocialAuth> builder)
    {
        builder.ToTable("IntegrationSocialAuths");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccessToken).IsRequired();
        builder.Property(x => x.Scopes).HasMaxLength(1000);
        builder.HasIndex(x => x.SocialAccountId).IsUnique();
    }
}

public class IntegrationSocialProfileConfiguration : IEntityTypeConfiguration<IntegrationSocialProfile>
{
    public void Configure(EntityTypeBuilder<IntegrationSocialProfile> builder)
    {
        builder.ToTable("IntegrationSocialProfiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalProfileId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.ExternalProfileId);
        builder.HasOne(x => x.SocialAccount).WithMany(x => x.Profiles).HasForeignKey(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class IntegrationPostConfiguration : IEntityTypeConfiguration<IntegrationPost>
{
    public void Configure(EntityTypeBuilder<IntegrationPost> builder)
    {
        builder.ToTable("IntegrationPosts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalPostId).HasMaxLength(200);
        builder.Property(x => x.Text).HasMaxLength(5000);
        builder.Property(x => x.Caption).HasMaxLength(5000);
        builder.HasOne(x => x.SocialProfile).WithMany(x => x.Posts).HasForeignKey(x => x.SocialProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class IntegrationMediaConfiguration : IEntityTypeConfiguration<IntegrationMedia>
{
    public void Configure(EntityTypeBuilder<IntegrationMedia> builder)
    {
        builder.ToTable("IntegrationMedia");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).IsRequired();
        builder.HasOne(x => x.Post).WithMany(x => x.MediaItems).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class IntegrationCommentConfiguration : IEntityTypeConfiguration<IntegrationComment>
{
    public void Configure(EntityTypeBuilder<IntegrationComment> builder)
    {
        builder.ToTable("IntegrationComments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalCommentId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.AuthorName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Message).IsRequired().HasMaxLength(5000);
        builder.HasIndex(x => x.ExternalCommentId).IsUnique();
        builder.HasOne(x => x.Post).WithMany(x => x.Comments).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ParentComment).WithMany(x => x.Replies).HasForeignKey(x => x.ParentCommentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class IntegrationConversationConfiguration : IEntityTypeConfiguration<IntegrationConversation>
{
    public void Configure(EntityTypeBuilder<IntegrationConversation> builder)
    {
        builder.ToTable("IntegrationConversations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalConversationId).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => new { x.SocialProfileId, x.ExternalConversationId }).IsUnique();
        builder.HasOne(x => x.SocialProfile).WithMany(x => x.Conversations).HasForeignKey(x => x.SocialProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class IntegrationMessageConfiguration : IEntityTypeConfiguration<IntegrationMessage>
{
    public void Configure(EntityTypeBuilder<IntegrationMessage> builder)
    {
        builder.ToTable("IntegrationMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalMessageId).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Body).HasMaxLength(5000);
        builder.Property(x => x.ReplyToExternalId).HasMaxLength(512);
        builder.HasIndex(x => x.ExternalMessageId).IsUnique();
        builder.HasOne(x => x.Conversation).WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class IntegrationMessageAttachmentConfiguration : IEntityTypeConfiguration<IntegrationMessageAttachment>
{
    public void Configure(EntityTypeBuilder<IntegrationMessageAttachment> builder)
    {
        builder.ToTable("IntegrationMessageAttachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).IsRequired().HasMaxLength(4000);
        builder.HasOne(x => x.Message).WithMany(x => x.Attachments).HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class IntegrationWebhookEventConfiguration : IEntityTypeConfiguration<IntegrationWebhookEvent>
{
    public void Configure(EntityTypeBuilder<IntegrationWebhookEvent> builder)
    {
        builder.ToTable("IntegrationWebhookEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.Status);
    }
}

public class IntegrationWebhookLogConfiguration : IEntityTypeConfiguration<IntegrationWebhookLog>
{
    public void Configure(EntityTypeBuilder<IntegrationWebhookLog> builder)
    {
        builder.ToTable("IntegrationWebhookLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlatformCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.ReceivedAt);
    }
}

public class IntegrationSyncJobConfiguration : IEntityTypeConfiguration<IntegrationSyncJob>
{
    public void Configure(EntityTypeBuilder<IntegrationSyncJob> builder)
    {
        builder.ToTable("IntegrationSyncJobs");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.SocialAccount).WithMany(x => x.SyncJobs).HasForeignKey(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AppConnectionPlatformConfiguration : IEntityTypeConfiguration<AppConnectionPlatform>
{
    public void Configure(EntityTypeBuilder<AppConnectionPlatform> builder)
    {
        builder.ToTable("AppConnectionPlatforms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Icon).HasMaxLength(500);
    }
}

public class AppConnectionSocialAccountConfiguration : IEntityTypeConfiguration<AppConnectionSocialAccount>
{
    public void Configure(EntityTypeBuilder<AppConnectionSocialAccount> builder)
    {
        builder.ToTable("AppConnectionSocialAccounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalAccountId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Username).HasMaxLength(200);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.HasIndex(x => new { x.UserId, x.PlatformId, x.ExternalAccountId }).IsUnique();
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Platform).WithMany(x => x.SocialAccounts).HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Auth).WithOne(x => x.SocialAccount).HasForeignKey<AppConnectionSocialAuth>(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AppConnectionSocialAuthConfiguration : IEntityTypeConfiguration<AppConnectionSocialAuth>
{
    public void Configure(EntityTypeBuilder<AppConnectionSocialAuth> builder)
    {
        builder.ToTable("AppConnectionSocialAuths");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccessToken).IsRequired();
        builder.Property(x => x.Scopes).HasMaxLength(1000);
        builder.HasIndex(x => x.SocialAccountId).IsUnique();
    }
}

public class AppConnectionSocialProfileConfiguration : IEntityTypeConfiguration<AppConnectionSocialProfile>
{
    public void Configure(EntityTypeBuilder<AppConnectionSocialProfile> builder)
    {
        builder.ToTable("AppConnectionSocialProfiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalProfileId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.ExternalProfileId);
        builder.HasOne(x => x.SocialAccount).WithMany(x => x.Profiles).HasForeignKey(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AppConnectionPostConfiguration : IEntityTypeConfiguration<AppConnectionPost>
{
    public void Configure(EntityTypeBuilder<AppConnectionPost> builder)
    {
        builder.ToTable("AppConnectionPosts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalPostId).HasMaxLength(200);
        builder.Property(x => x.Text).HasMaxLength(5000);
        builder.Property(x => x.Caption).HasMaxLength(5000);
        builder.HasOne(x => x.SocialProfile).WithMany(x => x.Posts).HasForeignKey(x => x.SocialProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AppConnectionMediaConfiguration : IEntityTypeConfiguration<AppConnectionMedia>
{
    public void Configure(EntityTypeBuilder<AppConnectionMedia> builder)
    {
        builder.ToTable("AppConnectionMedia");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).IsRequired();
        builder.HasOne(x => x.Post).WithMany(x => x.MediaItems).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AppConnectionCommentConfiguration : IEntityTypeConfiguration<AppConnectionComment>
{
    public void Configure(EntityTypeBuilder<AppConnectionComment> builder)
    {
        builder.ToTable("AppConnectionComments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalCommentId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.AuthorName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Message).IsRequired().HasMaxLength(5000);
        builder.HasIndex(x => x.ExternalCommentId).IsUnique();
        builder.HasOne(x => x.Post).WithMany(x => x.Comments).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ParentComment).WithMany(x => x.Replies).HasForeignKey(x => x.ParentCommentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AppConnectionConversationConfiguration : IEntityTypeConfiguration<AppConnectionConversation>
{
    public void Configure(EntityTypeBuilder<AppConnectionConversation> builder)
    {
        builder.ToTable("AppConnectionConversations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalConversationId).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => new { x.SocialProfileId, x.ExternalConversationId }).IsUnique();
        builder.HasOne(x => x.SocialProfile).WithMany(x => x.Conversations).HasForeignKey(x => x.SocialProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AppConnectionMessageConfiguration : IEntityTypeConfiguration<AppConnectionMessage>
{
    public void Configure(EntityTypeBuilder<AppConnectionMessage> builder)
    {
        builder.ToTable("AppConnectionMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalMessageId).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Body).HasMaxLength(5000);
        builder.Property(x => x.ReplyToExternalId).HasMaxLength(512);
        builder.HasIndex(x => x.ExternalMessageId).IsUnique();
        builder.HasOne(x => x.Conversation).WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AppConnectionMessageAttachmentConfiguration : IEntityTypeConfiguration<AppConnectionMessageAttachment>
{
    public void Configure(EntityTypeBuilder<AppConnectionMessageAttachment> builder)
    {
        builder.ToTable("AppConnectionMessageAttachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).IsRequired().HasMaxLength(4000);
        builder.HasOne(x => x.Message).WithMany(x => x.Attachments).HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AppConnectionWebhookEventConfiguration : IEntityTypeConfiguration<AppConnectionWebhookEvent>
{
    public void Configure(EntityTypeBuilder<AppConnectionWebhookEvent> builder)
    {
        builder.ToTable("AppConnectionWebhookEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.Status);
    }
}

public class AppConnectionWebhookLogConfiguration : IEntityTypeConfiguration<AppConnectionWebhookLog>
{
    public void Configure(EntityTypeBuilder<AppConnectionWebhookLog> builder)
    {
        builder.ToTable("AppConnectionWebhookLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlatformCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.ReceivedAt);
    }
}

public class AppConnectionSyncJobConfiguration : IEntityTypeConfiguration<AppConnectionSyncJob>
{
    public void Configure(EntityTypeBuilder<AppConnectionSyncJob> builder)
    {
        builder.ToTable("AppConnectionSyncJobs");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.SocialAccount).WithMany(x => x.SyncJobs).HasForeignKey(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DeveloperAppPlatformConfiguration : IEntityTypeConfiguration<DeveloperAppPlatform>
{
    public void Configure(EntityTypeBuilder<DeveloperAppPlatform> builder)
    {
        builder.ToTable("DeveloperAppPlatforms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Icon).HasMaxLength(500);
    }
}

public class DeveloperAppSocialAccountConfiguration : IEntityTypeConfiguration<DeveloperAppSocialAccount>
{
    public void Configure(EntityTypeBuilder<DeveloperAppSocialAccount> builder)
    {
        builder.ToTable("DeveloperAppSocialAccounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalAccountId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Username).HasMaxLength(200);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.HasIndex(x => new { x.UserId, x.PlatformId, x.ExternalAccountId }).IsUnique();
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Platform).WithMany(x => x.SocialAccounts).HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Auth).WithOne(x => x.SocialAccount).HasForeignKey<DeveloperAppSocialAuth>(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DeveloperAppSocialAuthConfiguration : IEntityTypeConfiguration<DeveloperAppSocialAuth>
{
    public void Configure(EntityTypeBuilder<DeveloperAppSocialAuth> builder)
    {
        builder.ToTable("DeveloperAppSocialAuths");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccessToken).IsRequired();
        builder.Property(x => x.Scopes).HasMaxLength(1000);
        builder.HasIndex(x => x.SocialAccountId).IsUnique();
    }
}

public class DeveloperAppSocialProfileConfiguration : IEntityTypeConfiguration<DeveloperAppSocialProfile>
{
    public void Configure(EntityTypeBuilder<DeveloperAppSocialProfile> builder)
    {
        builder.ToTable("DeveloperAppSocialProfiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalProfileId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.ExternalProfileId);
        builder.HasOne(x => x.SocialAccount).WithMany(x => x.Profiles).HasForeignKey(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DeveloperAppPostConfiguration : IEntityTypeConfiguration<DeveloperAppPost>
{
    public void Configure(EntityTypeBuilder<DeveloperAppPost> builder)
    {
        builder.ToTable("DeveloperAppPosts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalPostId).HasMaxLength(200);
        builder.Property(x => x.Text).HasMaxLength(5000);
        builder.Property(x => x.Caption).HasMaxLength(5000);
        builder.HasOne(x => x.SocialProfile).WithMany(x => x.Posts).HasForeignKey(x => x.SocialProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DeveloperAppMediaConfiguration : IEntityTypeConfiguration<DeveloperAppMedia>
{
    public void Configure(EntityTypeBuilder<DeveloperAppMedia> builder)
    {
        builder.ToTable("DeveloperAppMedia");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).IsRequired();
        builder.HasOne(x => x.Post).WithMany(x => x.MediaItems).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DeveloperAppCommentConfiguration : IEntityTypeConfiguration<DeveloperAppComment>
{
    public void Configure(EntityTypeBuilder<DeveloperAppComment> builder)
    {
        builder.ToTable("DeveloperAppComments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalCommentId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.AuthorName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Message).IsRequired().HasMaxLength(5000);
        builder.HasIndex(x => x.ExternalCommentId).IsUnique();
        builder.HasOne(x => x.Post).WithMany(x => x.Comments).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ParentComment).WithMany(x => x.Replies).HasForeignKey(x => x.ParentCommentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DeveloperAppConversationConfiguration : IEntityTypeConfiguration<DeveloperAppConversation>
{
    public void Configure(EntityTypeBuilder<DeveloperAppConversation> builder)
    {
        builder.ToTable("DeveloperAppConversations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalConversationId).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => new { x.SocialProfileId, x.ExternalConversationId }).IsUnique();
        builder.HasOne(x => x.SocialProfile).WithMany(x => x.Conversations).HasForeignKey(x => x.SocialProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DeveloperAppMessageConfiguration : IEntityTypeConfiguration<DeveloperAppMessage>
{
    public void Configure(EntityTypeBuilder<DeveloperAppMessage> builder)
    {
        builder.ToTable("DeveloperAppMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalMessageId).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Body).HasMaxLength(5000);
        builder.Property(x => x.ReplyToExternalId).HasMaxLength(512);
        builder.HasIndex(x => x.ExternalMessageId).IsUnique();
        builder.HasOne(x => x.Conversation).WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DeveloperAppMessageAttachmentConfiguration : IEntityTypeConfiguration<DeveloperAppMessageAttachment>
{
    public void Configure(EntityTypeBuilder<DeveloperAppMessageAttachment> builder)
    {
        builder.ToTable("DeveloperAppMessageAttachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).IsRequired().HasMaxLength(4000);
        builder.HasOne(x => x.Message).WithMany(x => x.Attachments).HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DeveloperAppWebhookEventConfiguration : IEntityTypeConfiguration<DeveloperAppWebhookEvent>
{
    public void Configure(EntityTypeBuilder<DeveloperAppWebhookEvent> builder)
    {
        builder.ToTable("DeveloperAppWebhookEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.Status);
    }
}

public class DeveloperAppWebhookLogConfiguration : IEntityTypeConfiguration<DeveloperAppWebhookLog>
{
    public void Configure(EntityTypeBuilder<DeveloperAppWebhookLog> builder)
    {
        builder.ToTable("DeveloperAppWebhookLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlatformCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.ReceivedAt);
    }
}

public class DeveloperAppSyncJobConfiguration : IEntityTypeConfiguration<DeveloperAppSyncJob>
{
    public void Configure(EntityTypeBuilder<DeveloperAppSyncJob> builder)
    {
        builder.ToTable("DeveloperAppSyncJobs");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.SocialAccount).WithMany(x => x.SyncJobs).HasForeignKey(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}


public class SocialMediaAccountPlatformConfiguration : IEntityTypeConfiguration<SocialMediaAccountPlatform>
{
    public void Configure(EntityTypeBuilder<SocialMediaAccountPlatform> builder)
    {
        builder.ToTable("SocialMediaAccountPlatforms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Icon).HasMaxLength(500);
    }
}

public class SocialMediaAccountSocialAccountConfiguration : IEntityTypeConfiguration<SocialMediaAccountSocialAccount>
{
    public void Configure(EntityTypeBuilder<SocialMediaAccountSocialAccount> builder)
    {
        builder.ToTable("SocialMediaAccountSocialAccounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalAccountId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Username).HasMaxLength(200);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.HasIndex(x => new { x.UserId, x.PlatformId, x.ExternalAccountId }).IsUnique();
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Platform).WithMany(x => x.SocialAccounts).HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Auth).WithOne(x => x.SocialAccount).HasForeignKey<SocialMediaAccountSocialAuth>(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SocialMediaAccountSocialAuthConfiguration : IEntityTypeConfiguration<SocialMediaAccountSocialAuth>
{
    public void Configure(EntityTypeBuilder<SocialMediaAccountSocialAuth> builder)
    {
        builder.ToTable("SocialMediaAccountSocialAuths");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccessToken).IsRequired();
        builder.Property(x => x.Scopes).HasMaxLength(1000);
        builder.HasIndex(x => x.SocialAccountId).IsUnique();
    }
}

public class SocialMediaAccountSocialProfileConfiguration : IEntityTypeConfiguration<SocialMediaAccountSocialProfile>
{
    public void Configure(EntityTypeBuilder<SocialMediaAccountSocialProfile> builder)
    {
        builder.ToTable("SocialMediaAccountSocialProfiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalProfileId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.ExternalProfileId);
        builder.HasOne(x => x.SocialAccount).WithMany(x => x.Profiles).HasForeignKey(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SocialMediaAccountPostConfiguration : IEntityTypeConfiguration<SocialMediaAccountPost>
{
    public void Configure(EntityTypeBuilder<SocialMediaAccountPost> builder)
    {
        builder.ToTable("SocialMediaAccountPosts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalPostId).HasMaxLength(200);
        builder.Property(x => x.Text).HasMaxLength(5000);
        builder.Property(x => x.Caption).HasMaxLength(5000);
        builder.HasOne(x => x.SocialProfile).WithMany(x => x.Posts).HasForeignKey(x => x.SocialProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SocialMediaAccountMediaConfiguration : IEntityTypeConfiguration<SocialMediaAccountMedia>
{
    public void Configure(EntityTypeBuilder<SocialMediaAccountMedia> builder)
    {
        builder.ToTable("SocialMediaAccountMedia");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).IsRequired();
        builder.HasOne(x => x.Post).WithMany(x => x.MediaItems).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SocialMediaAccountCommentConfiguration : IEntityTypeConfiguration<SocialMediaAccountComment>
{
    public void Configure(EntityTypeBuilder<SocialMediaAccountComment> builder)
    {
        builder.ToTable("SocialMediaAccountComments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalCommentId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.AuthorName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Message).IsRequired().HasMaxLength(5000);
        builder.HasIndex(x => x.ExternalCommentId).IsUnique();
        builder.HasOne(x => x.Post).WithMany(x => x.Comments).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ParentComment).WithMany(x => x.Replies).HasForeignKey(x => x.ParentCommentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SocialMediaAccountConversationConfiguration : IEntityTypeConfiguration<SocialMediaAccountConversation>
{
    public void Configure(EntityTypeBuilder<SocialMediaAccountConversation> builder)
    {
        builder.ToTable("SocialMediaAccountConversations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalConversationId).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => new { x.SocialProfileId, x.ExternalConversationId }).IsUnique();
        builder.HasOne(x => x.SocialProfile).WithMany(x => x.Conversations).HasForeignKey(x => x.SocialProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SocialMediaAccountMessageConfiguration : IEntityTypeConfiguration<SocialMediaAccountMessage>
{
    public void Configure(EntityTypeBuilder<SocialMediaAccountMessage> builder)
    {
        builder.ToTable("SocialMediaAccountMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalMessageId).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Body).HasMaxLength(5000);
        builder.Property(x => x.ReplyToExternalId).HasMaxLength(512);
        builder.HasIndex(x => x.ExternalMessageId).IsUnique();
        builder.HasOne(x => x.Conversation).WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SocialMediaAccountMessageAttachmentConfiguration : IEntityTypeConfiguration<SocialMediaAccountMessageAttachment>
{
    public void Configure(EntityTypeBuilder<SocialMediaAccountMessageAttachment> builder)
    {
        builder.ToTable("SocialMediaAccountMessageAttachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).IsRequired().HasMaxLength(4000);
        builder.HasOne(x => x.Message).WithMany(x => x.Attachments).HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SocialMediaAccountWebhookEventConfiguration : IEntityTypeConfiguration<SocialMediaAccountWebhookEvent>
{
    public void Configure(EntityTypeBuilder<SocialMediaAccountWebhookEvent> builder)
    {
        builder.ToTable("SocialMediaAccountWebhookEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.Status);
    }
}

public class SocialMediaAccountWebhookLogConfiguration : IEntityTypeConfiguration<SocialMediaAccountWebhookLog>
{
    public void Configure(EntityTypeBuilder<SocialMediaAccountWebhookLog> builder)
    {
        builder.ToTable("SocialMediaAccountWebhookLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlatformCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.ReceivedAt);
    }
}

public class SocialMediaAccountSyncJobConfiguration : IEntityTypeConfiguration<SocialMediaAccountSyncJob>
{
    public void Configure(EntityTypeBuilder<SocialMediaAccountSyncJob> builder)
    {
        builder.ToTable("SocialMediaAccountSyncJobs");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.SocialAccount).WithMany(x => x.SyncJobs).HasForeignKey(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AccountConnectionPlatformConfiguration : IEntityTypeConfiguration<AccountConnectionPlatform>
{
    public void Configure(EntityTypeBuilder<AccountConnectionPlatform> builder)
    {
        builder.ToTable("AccountConnectionPlatforms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Icon).HasMaxLength(500);
    }
}
public class AccountManagementPlatformConfiguration : IEntityTypeConfiguration<AccountManagementPlatform>
{
    public void Configure(EntityTypeBuilder<AccountManagementPlatform> builder)
    {
        builder.ToTable("AccountManagementPlatforms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Icon).HasMaxLength(500);
    }
}
public class AccountHubPlatformConfiguration : IEntityTypeConfiguration<AccountHubPlatform>
{
    public void Configure(EntityTypeBuilder<AccountHubPlatform> builder)
    {
        builder.ToTable("AccountHubPlatforms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Icon).HasMaxLength(500);
    }
}

public class AccountConnectionSocialAccountConfiguration : IEntityTypeConfiguration<AccountConnectionSocialAccount>
{
    public void Configure(EntityTypeBuilder<AccountConnectionSocialAccount> builder)
    {
        builder.ToTable("AccountConnectionSocialAccounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalAccountId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Username).HasMaxLength(200);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.HasIndex(x => new { x.UserId, x.PlatformId, x.ExternalAccountId }).IsUnique();
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Platform).WithMany(x => x.SocialAccounts).HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Auth).WithOne(x => x.SocialAccount).HasForeignKey<AccountConnectionSocialAuth>(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}
public class AccountManagementSocialAccountConfiguration : IEntityTypeConfiguration<AccountManagementSocialAccount>
{
    public void Configure(EntityTypeBuilder<AccountManagementSocialAccount> builder)
    {
        builder.ToTable("AccountManagementSocialAccounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalAccountId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Username).HasMaxLength(200);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.HasIndex(x => new { x.UserId, x.PlatformId, x.ExternalAccountId }).IsUnique();
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Platform).WithMany(x => x.SocialAccounts).HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Auth).WithOne(x => x.SocialAccount).HasForeignKey<AccountManagementSocialAuth>(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}
public class AccountHubSocialAccountConfiguration : IEntityTypeConfiguration<AccountHubSocialAccount>
{
    public void Configure(EntityTypeBuilder<AccountHubSocialAccount> builder)
    {
        builder.ToTable("AccountHubSocialAccounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalAccountId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Username).HasMaxLength(200);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.HasIndex(x => new { x.UserId, x.PlatformId, x.ExternalAccountId }).IsUnique();
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Platform).WithMany(x => x.SocialAccounts).HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Auth).WithOne(x => x.SocialAccount).HasForeignKey<AccountHubSocialAuth>(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AccountConnectionSocialAuthConfiguration : IEntityTypeConfiguration<AccountConnectionSocialAuth>
{
    public void Configure(EntityTypeBuilder<AccountConnectionSocialAuth> builder)
    {
        builder.ToTable("AccountConnectionSocialAuths");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccessToken).IsRequired();
        builder.Property(x => x.Scopes).HasMaxLength(1000);
        builder.HasIndex(x => x.SocialAccountId).IsUnique();
    }
}
public class AccountManagementSocialAuthConfiguration : IEntityTypeConfiguration<AccountManagementSocialAuth>
{
    public void Configure(EntityTypeBuilder<AccountManagementSocialAuth> builder)
    {
        builder.ToTable("AccountManagementSocialAuths");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccessToken).IsRequired();
        builder.Property(x => x.Scopes).HasMaxLength(1000);
        builder.HasIndex(x => x.SocialAccountId).IsUnique();
    }
}
public class AccountHubSocialAuthConfiguration : IEntityTypeConfiguration<AccountHubSocialAuth>
{
    public void Configure(EntityTypeBuilder<AccountHubSocialAuth> builder)
    {
        builder.ToTable("AccountHubSocialAuths");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccessToken).IsRequired();
        builder.Property(x => x.Scopes).HasMaxLength(1000);
        builder.HasIndex(x => x.SocialAccountId).IsUnique();
    }
}

public class AccountConnectionSocialProfileConfiguration : IEntityTypeConfiguration<AccountConnectionSocialProfile>
{
    public void Configure(EntityTypeBuilder<AccountConnectionSocialProfile> builder)
    {
        builder.ToTable("AccountConnectionSocialProfiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalProfileId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.ExternalProfileId);
        builder.HasOne(x => x.SocialAccount).WithMany(x => x.Profiles).HasForeignKey(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}
public class AccountManagementSocialProfileConfiguration : IEntityTypeConfiguration<AccountManagementSocialProfile>
{
    public void Configure(EntityTypeBuilder<AccountManagementSocialProfile> builder)
    {
        builder.ToTable("AccountManagementSocialProfiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalProfileId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.ExternalProfileId);
        builder.HasOne(x => x.SocialAccount).WithMany(x => x.Profiles).HasForeignKey(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}
public class AccountHubSocialProfileConfiguration : IEntityTypeConfiguration<AccountHubSocialProfile>
{
    public void Configure(EntityTypeBuilder<AccountHubSocialProfile> builder)
    {
        builder.ToTable("AccountHubSocialProfiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalProfileId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.ExternalProfileId);
        builder.HasOne(x => x.SocialAccount).WithMany(x => x.Profiles).HasForeignKey(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AccountConnectionPostConfiguration : IEntityTypeConfiguration<AccountConnectionPost>
{
    public void Configure(EntityTypeBuilder<AccountConnectionPost> builder)
    {
        builder.ToTable("AccountConnectionPosts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalPostId).HasMaxLength(200);
        builder.Property(x => x.Text).HasMaxLength(5000);
        builder.Property(x => x.Caption).HasMaxLength(5000);
        builder.HasOne(x => x.SocialProfile).WithMany(x => x.Posts).HasForeignKey(x => x.SocialProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Restrict);
    }
}
public class AccountManagementPostConfiguration : IEntityTypeConfiguration<AccountManagementPost>
{
    public void Configure(EntityTypeBuilder<AccountManagementPost> builder)
    {
        builder.ToTable("AccountManagementPosts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalPostId).HasMaxLength(200);
        builder.Property(x => x.Text).HasMaxLength(5000);
        builder.Property(x => x.Caption).HasMaxLength(5000);
        builder.HasOne(x => x.SocialProfile).WithMany(x => x.Posts).HasForeignKey(x => x.SocialProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Restrict);
    }
}
public class AccountHubPostConfiguration : IEntityTypeConfiguration<AccountHubPost>
{
    public void Configure(EntityTypeBuilder<AccountHubPost> builder)
    {
        builder.ToTable("AccountHubPosts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalPostId).HasMaxLength(200);
        builder.Property(x => x.Text).HasMaxLength(5000);
        builder.Property(x => x.Caption).HasMaxLength(5000);
        builder.HasOne(x => x.SocialProfile).WithMany(x => x.Posts).HasForeignKey(x => x.SocialProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AccountConnectionMediaConfiguration : IEntityTypeConfiguration<AccountConnectionMedia>
{
    public void Configure(EntityTypeBuilder<AccountConnectionMedia> builder)
    {
        builder.ToTable("AccountConnectionMedia");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).IsRequired();
        builder.HasOne(x => x.Post).WithMany(x => x.MediaItems).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
    }
}
public class AccountManagementMediaConfiguration : IEntityTypeConfiguration<AccountManagementMedia>
{
    public void Configure(EntityTypeBuilder<AccountManagementMedia> builder)
    {
        builder.ToTable("AccountManagementMedia");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).IsRequired();
        builder.HasOne(x => x.Post).WithMany(x => x.MediaItems).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
    }
}
public class AccountHubMediaConfiguration : IEntityTypeConfiguration<AccountHubMedia>
{
    public void Configure(EntityTypeBuilder<AccountHubMedia> builder)
    {
        builder.ToTable("AccountHubMedia");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).IsRequired();
        builder.HasOne(x => x.Post).WithMany(x => x.MediaItems).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AccountConnectionCommentConfiguration : IEntityTypeConfiguration<AccountConnectionComment>
{
    public void Configure(EntityTypeBuilder<AccountConnectionComment> builder)
    {
        builder.ToTable("AccountConnectionComments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalCommentId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.AuthorName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Message).IsRequired().HasMaxLength(5000);
        builder.HasIndex(x => x.ExternalCommentId).IsUnique();
        builder.HasOne(x => x.Post).WithMany(x => x.Comments).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ParentComment).WithMany(x => x.Replies).HasForeignKey(x => x.ParentCommentId).OnDelete(DeleteBehavior.Restrict);
    }
}
public class AccountManagementCommentConfiguration : IEntityTypeConfiguration<AccountManagementComment>
{
    public void Configure(EntityTypeBuilder<AccountManagementComment> builder)
    {
        builder.ToTable("AccountManagementComments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalCommentId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.AuthorName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Message).IsRequired().HasMaxLength(5000);
        builder.HasIndex(x => x.ExternalCommentId).IsUnique();
        builder.HasOne(x => x.Post).WithMany(x => x.Comments).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ParentComment).WithMany(x => x.Replies).HasForeignKey(x => x.ParentCommentId).OnDelete(DeleteBehavior.Restrict);
    }
}
public class AccountHubCommentConfiguration : IEntityTypeConfiguration<AccountHubComment>
{
    public void Configure(EntityTypeBuilder<AccountHubComment> builder)
    {
        builder.ToTable("AccountHubComments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalCommentId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.AuthorName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Message).IsRequired().HasMaxLength(5000);
        builder.HasIndex(x => x.ExternalCommentId).IsUnique();
        builder.HasOne(x => x.Post).WithMany(x => x.Comments).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ParentComment).WithMany(x => x.Replies).HasForeignKey(x => x.ParentCommentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AccountConnectionConversationConfiguration : IEntityTypeConfiguration<AccountConnectionConversation>
{
    public void Configure(EntityTypeBuilder<AccountConnectionConversation> builder)
    {
        builder.ToTable("AccountConnectionConversations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalConversationId).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => new { x.SocialProfileId, x.ExternalConversationId }).IsUnique();
        builder.HasOne(x => x.SocialProfile).WithMany(x => x.Conversations).HasForeignKey(x => x.SocialProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}
public class AccountManagementConversationConfiguration : IEntityTypeConfiguration<AccountManagementConversation>
{
    public void Configure(EntityTypeBuilder<AccountManagementConversation> builder)
    {
        builder.ToTable("AccountManagementConversations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalConversationId).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => new { x.SocialProfileId, x.ExternalConversationId }).IsUnique();
        builder.HasOne(x => x.SocialProfile).WithMany(x => x.Conversations).HasForeignKey(x => x.SocialProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}
public class AccountHubConversationConfiguration : IEntityTypeConfiguration<AccountHubConversation>
{
    public void Configure(EntityTypeBuilder<AccountHubConversation> builder)
    {
        builder.ToTable("AccountHubConversations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalConversationId).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => new { x.SocialProfileId, x.ExternalConversationId }).IsUnique();
        builder.HasOne(x => x.SocialProfile).WithMany(x => x.Conversations).HasForeignKey(x => x.SocialProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AccountConnectionMessageConfiguration : IEntityTypeConfiguration<AccountConnectionMessage>
{
    public void Configure(EntityTypeBuilder<AccountConnectionMessage> builder)
    {
        builder.ToTable("AccountConnectionMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalMessageId).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Body).HasMaxLength(5000);
        builder.Property(x => x.ReplyToExternalId).HasMaxLength(512);
        builder.HasIndex(x => x.ExternalMessageId).IsUnique();
        builder.HasOne(x => x.Conversation).WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}
public class AccountManagementMessageConfiguration : IEntityTypeConfiguration<AccountManagementMessage>
{
    public void Configure(EntityTypeBuilder<AccountManagementMessage> builder)
    {
        builder.ToTable("AccountManagementMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalMessageId).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Body).HasMaxLength(5000);
        builder.Property(x => x.ReplyToExternalId).HasMaxLength(512);
        builder.HasIndex(x => x.ExternalMessageId).IsUnique();
        builder.HasOne(x => x.Conversation).WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}
public class AccountHubMessageConfiguration : IEntityTypeConfiguration<AccountHubMessage>
{
    public void Configure(EntityTypeBuilder<AccountHubMessage> builder)
    {
        builder.ToTable("AccountHubMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalMessageId).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Body).HasMaxLength(5000);
        builder.Property(x => x.ReplyToExternalId).HasMaxLength(512);
        builder.HasIndex(x => x.ExternalMessageId).IsUnique();
        builder.HasOne(x => x.Conversation).WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AccountConnectionMessageAttachmentConfiguration : IEntityTypeConfiguration<AccountConnectionMessageAttachment>
{
    public void Configure(EntityTypeBuilder<AccountConnectionMessageAttachment> builder)
    {
        builder.ToTable("AccountConnectionMessageAttachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).IsRequired().HasMaxLength(4000);
        builder.HasOne(x => x.Message).WithMany(x => x.Attachments).HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}
public class AccountManagementMessageAttachmentConfiguration : IEntityTypeConfiguration<AccountManagementMessageAttachment>
{
    public void Configure(EntityTypeBuilder<AccountManagementMessageAttachment> builder)
    {
        builder.ToTable("AccountManagementMessageAttachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).IsRequired().HasMaxLength(4000);
        builder.HasOne(x => x.Message).WithMany(x => x.Attachments).HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}
public class AccountHubMessageAttachmentConfiguration : IEntityTypeConfiguration<AccountHubMessageAttachment>
{
    public void Configure(EntityTypeBuilder<AccountHubMessageAttachment> builder)
    {
        builder.ToTable("AccountHubMessageAttachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).IsRequired().HasMaxLength(4000);
        builder.HasOne(x => x.Message).WithMany(x => x.Attachments).HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AccountConnectionWebhookEventConfiguration : IEntityTypeConfiguration<AccountConnectionWebhookEvent>
{
    public void Configure(EntityTypeBuilder<AccountConnectionWebhookEvent> builder)
    {
        builder.ToTable("AccountConnectionWebhookEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.Status);
    }
}
public class AccountManagementWebhookEventConfiguration : IEntityTypeConfiguration<AccountManagementWebhookEvent>
{
    public void Configure(EntityTypeBuilder<AccountManagementWebhookEvent> builder)
    {
        builder.ToTable("AccountManagementWebhookEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.Status);
    }
}
public class AccountHubWebhookEventConfiguration : IEntityTypeConfiguration<AccountHubWebhookEvent>
{
    public void Configure(EntityTypeBuilder<AccountHubWebhookEvent> builder)
    {
        builder.ToTable("AccountHubWebhookEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.Status);
    }
}

public class AccountConnectionWebhookLogConfiguration : IEntityTypeConfiguration<AccountConnectionWebhookLog>
{
    public void Configure(EntityTypeBuilder<AccountConnectionWebhookLog> builder)
    {
        builder.ToTable("AccountConnectionWebhookLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlatformCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.ReceivedAt);
    }
}
public class AccountManagementWebhookLogConfiguration : IEntityTypeConfiguration<AccountManagementWebhookLog>
{
    public void Configure(EntityTypeBuilder<AccountManagementWebhookLog> builder)
    {
        builder.ToTable("AccountManagementWebhookLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlatformCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.ReceivedAt);
    }
}
public class AccountHubWebhookLogConfiguration : IEntityTypeConfiguration<AccountHubWebhookLog>
{
    public void Configure(EntityTypeBuilder<AccountHubWebhookLog> builder)
    {
        builder.ToTable("AccountHubWebhookLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlatformCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.ReceivedAt);
    }
}

public class AccountConnectionSyncJobConfiguration : IEntityTypeConfiguration<AccountConnectionSyncJob>
{
    public void Configure(EntityTypeBuilder<AccountConnectionSyncJob> builder)
    {
        builder.ToTable("AccountConnectionSyncJobs");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.SocialAccount).WithMany(x => x.SyncJobs).HasForeignKey(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}
public class AccountManagementSyncJobConfiguration : IEntityTypeConfiguration<AccountManagementSyncJob>
{
    public void Configure(EntityTypeBuilder<AccountManagementSyncJob> builder)
    {
        builder.ToTable("AccountManagementSyncJobs");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.SocialAccount).WithMany(x => x.SyncJobs).HasForeignKey(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}
public class AccountHubSyncJobConfiguration : IEntityTypeConfiguration<AccountHubSyncJob>
{
    public void Configure(EntityTypeBuilder<AccountHubSyncJob> builder)
    {
        builder.ToTable("AccountHubSyncJobs");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.SocialAccount).WithMany(x => x.SyncJobs).HasForeignKey(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}
