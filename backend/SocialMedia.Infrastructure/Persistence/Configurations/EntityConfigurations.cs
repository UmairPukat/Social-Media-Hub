using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocialMedia.Domain.Entities;

namespace SocialMedia.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Role).IsRequired().HasMaxLength(50);
    }
}

public class AccessTokenConfiguration : IEntityTypeConfiguration<AccessToken>
{
    public void Configure(EntityTypeBuilder<AccessToken> builder)
    {
        builder.ToTable("AccessTokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Token).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.Token).IsUnique();
    }
}

public class PlatformConfiguration : IEntityTypeConfiguration<Platform>
{
    public void Configure(EntityTypeBuilder<Platform> builder)
    {
        builder.ToTable("Platforms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Icon).HasMaxLength(500);
    }
}

public class SocialAccountConfiguration : IEntityTypeConfiguration<SocialAccount>
{
    public void Configure(EntityTypeBuilder<SocialAccount> builder)
    {
        builder.ToTable("SocialAccounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalAccountId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Username).HasMaxLength(200);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.HasIndex(x => new { x.UserId, x.PlatformId }).IsUnique();

        builder.HasOne(x => x.User).WithMany(x => x.SocialAccounts).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Platform).WithMany(x => x.SocialAccounts).HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Auth).WithOne(x => x.SocialAccount).HasForeignKey<SocialAuth>(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SocialAuthConfiguration : IEntityTypeConfiguration<SocialAuth>
{
    public void Configure(EntityTypeBuilder<SocialAuth> builder)
    {
        builder.ToTable("SocialAuths");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccessToken).IsRequired();
        builder.Property(x => x.Scopes).HasMaxLength(1000);
        builder.HasIndex(x => x.SocialAccountId).IsUnique();
    }
}

public class SocialProfileConfiguration : IEntityTypeConfiguration<SocialProfile>
{
    public void Configure(EntityTypeBuilder<SocialProfile> builder)
    {
        builder.ToTable("SocialProfiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalProfileId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.ExternalProfileId);
        builder.HasOne(x => x.SocialAccount).WithMany(x => x.Profiles).HasForeignKey(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("Posts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalPostId).HasMaxLength(200);
        builder.Property(x => x.Text).HasMaxLength(5000);
        builder.Property(x => x.Caption).HasMaxLength(5000);
        builder.HasOne(x => x.SocialProfile).WithMany(x => x.Posts).HasForeignKey(x => x.SocialProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MediaConfiguration : IEntityTypeConfiguration<Media>
{
    public void Configure(EntityTypeBuilder<Media> builder)
    {
        builder.ToTable("Media");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).IsRequired().HasMaxLength(2000);
        builder.HasOne(x => x.Post).WithMany(x => x.MediaItems).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalCommentId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.AuthorName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Message).IsRequired().HasMaxLength(5000);
        builder.HasOne(x => x.Post).WithMany(x => x.Comments).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ParentComment).WithMany(x => x.Replies).HasForeignKey(x => x.ParentCommentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalConversationId).IsRequired().HasMaxLength(200);
        builder.HasOne(x => x.SocialProfile).WithMany(x => x.Conversations).HasForeignKey(x => x.SocialProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalMessageId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Body).HasMaxLength(5000);
        builder.HasOne(x => x.Conversation).WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MessageAttachmentConfiguration : IEntityTypeConfiguration<MessageAttachment>
{
    public void Configure(EntityTypeBuilder<MessageAttachment> builder)
    {
        builder.ToTable("MessageAttachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).IsRequired().HasMaxLength(2000);
        builder.HasOne(x => x.Message).WithMany(x => x.Attachments).HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.ToTable("WebhookEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.Status);
    }
}

public class WebhookLogConfiguration : IEntityTypeConfiguration<WebhookLog>
{
    public void Configure(EntityTypeBuilder<WebhookLog> builder)
    {
        builder.ToTable("WebhookLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlatformCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.ReceivedAt);
    }
}

public class SyncJobConfiguration : IEntityTypeConfiguration<SyncJob>
{
    public void Configure(EntityTypeBuilder<SyncJob> builder)
    {
        builder.ToTable("SyncJobs");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.SocialAccount).WithMany(x => x.SyncJobs).HasForeignKey(x => x.SocialAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}
