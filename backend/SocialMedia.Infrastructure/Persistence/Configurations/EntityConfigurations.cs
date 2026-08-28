using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.AppConnections.Entities;
using SocialMedia.Domain.Modules.DeveloperApps.Entities;
using SocialMedia.Domain.Modules.Integrations.Entities;

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

public class AppConnectionConfigConfiguration : IEntityTypeConfiguration<AppConnectionConfig>
{
    public void Configure(EntityTypeBuilder<AppConnectionConfig> builder)
    {
        builder.ToTable("AppConnectionConfigs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlatformCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.MenuType).IsRequired().HasMaxLength(50).HasDefaultValue("app_connection");
        builder.Property(x => x.Label).HasMaxLength(200);
        builder.Property(x => x.ClientId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ClientSecret).IsRequired();
        builder.Property(x => x.RedirectUri).HasMaxLength(2000);
        builder.Property(x => x.AuthUrl).HasMaxLength(2000);
        builder.Property(x => x.BaseUrl).HasMaxLength(500);
        builder.Property(x => x.Scopes).HasMaxLength(2000);
        builder.Property(x => x.GraphApiVersion).IsRequired().HasMaxLength(20).HasDefaultValue("v21.0");
        builder.Property(x => x.WebhookVerifyToken).HasMaxLength(500);
        builder.Property(x => x.PhoneNumberId).HasMaxLength(100);
        builder.Property(x => x.WabaId).HasMaxLength(100);
        builder.HasIndex(x => new { x.UserId, x.PlatformId, x.MenuType }).IsUnique();
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class IntegrationAppConfigConfiguration : IEntityTypeConfiguration<IntegrationAppConfig>
{
    public void Configure(EntityTypeBuilder<IntegrationAppConfig> builder)
    {
        builder.ToTable("IntegrationAppConfigs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlatformCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.MenuType).IsRequired().HasMaxLength(50).HasDefaultValue("integration");
        builder.Property(x => x.ClientId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ClientSecret).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.PlatformId, x.MenuType }).IsUnique();
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DeveloperAppConfigConfiguration : IEntityTypeConfiguration<DeveloperAppConfig>
{
    public void Configure(EntityTypeBuilder<DeveloperAppConfig> builder)
    {
        builder.ToTable("DeveloperAppConfigs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlatformCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.MenuType).IsRequired().HasMaxLength(50).HasDefaultValue("developer_app");
        builder.Property(x => x.ClientId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ClientSecret).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.PlatformId, x.MenuType }).IsUnique();
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Platform).WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Cascade);
    }
}
