using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialMedia.Application.Catalog;
using SocialMedia.Domain.Entities;

namespace SocialMedia.Infrastructure.Persistence;

/// <summary>
/// Seeds platforms from <see cref="PlatformCatalog"/>, invite token, and default admin.
/// </summary>
public static class DbSeeder
{
    public static readonly Guid FacebookPlatformId = PlatformCatalog.FacebookId;
    public static readonly Guid InstagramPlatformId = PlatformCatalog.InstagramId;
    public static readonly Guid WhatsAppPlatformId = PlatformCatalog.WhatsAppId;

    /// <summary>
    /// Seeds the database, retrying on transient connection failures. This is useful in
    /// containerized environments (e.g. Railway) where the SQL Server service may not be
    /// ready yet when the API container starts.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="maxAttempts">Maximum number of attempts before giving up.</param>
    /// <param name="delayBetweenAttempts">Delay to wait between failed attempts.</param>
    /// <param name="logger">Optional logger for retry diagnostics.</param>
    public static async Task SeedWithRetryAsync(
        AppDbContext db,
        int maxAttempts = 8,
        TimeSpan? delayBetweenAttempts = null,
        ILogger? logger = null)
    {
        var delay = delayBetweenAttempts ?? TimeSpan.FromSeconds(3);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await SeedAsync(db);
                if (attempt > 1)
                {
                    logger?.LogInformation("Database seeding succeeded on attempt {Attempt}.", attempt);
                }
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger?.LogWarning(
                    ex,
                    "Database seeding attempt {Attempt}/{MaxAttempts} failed. Retrying in {Delay}s...",
                    attempt,
                    maxAttempts,
                    delay.TotalSeconds);

                await Task.Delay(delay);
            }
        }

        // Final attempt: let any exception propagate so the caller can decide how to handle it.
        await SeedAsync(db);
    }

    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await EnsureWebhookLogsTableAsync(db);
        await EnsureMessageReplyColumnsAsync(db);
        await EnsureMenuTypeColumnsAsync(db);
        await EnsureAppConnectionConfigsTableAsync(db);
        await EnsureIntegrationAppConfigsTableAsync(db);
        await EnsureDeveloperAppConfigsTableAsync(db);

        var catalogCodes = new HashSet<string>(
            PlatformCatalog.All.Select(p => p.Code),
            StringComparer.OrdinalIgnoreCase);

        foreach (var def in PlatformCatalog.All)
        {
            foreach (var menuType in ProcessModules.AllMenuTypes)
            {
                var platformId = PlatformCatalog.IdForMenu(def.Id, menuType);
                var existing = db.Platforms.FirstOrDefault(p => p.Id == platformId)
                    ?? db.Platforms.FirstOrDefault(p => p.Code == def.Code && p.MenuType == menuType);

                if (existing is null)
                {
                    db.Platforms.Add(new Platform
                    {
                        Id = platformId,
                        Name = def.Name,
                        Code = def.Code,
                        Icon = def.Icon,
                        MenuType = menuType,
                        IsActive = true
                    });
                }
                else
                {
                    existing.Name = def.Name;
                    existing.Icon = def.Icon;
                    existing.MenuType = menuType;
                    existing.IsActive = true;
                }
            }
        }

        // Hide platforms that are no longer in the catalog.
        foreach (var orphan in db.Platforms.Where(p => !catalogCodes.Contains(p.Code)))
        {
            orphan.IsActive = false;
        }

        if (!db.AccessTokens.Any())
        {
            db.AccessTokens.Add(new AccessToken
            {
                Token = "INVITE-SOCIALHUB-2026",
                Label = "Default invite token",
                IsUsed = false,
                ExpiresAt = DateTime.UtcNow.AddYears(1)
            });
        }

        const string adminEmail = "admin@gmail.com";
        if (!db.Users.Any(u => u.Email == adminEmail))
        {
            db.Users.Add(new User
            {
                Email = adminEmail,
                FullName = "Platform Admin",
                Role = "Admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@321"),
                IsActive = true
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// EnsureCreated does not alter existing databases — create WebhookLogs if missing.
    /// </summary>
    private static async Task EnsureWebhookLogsTableAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "WebhookLogs" (
                "Id" uuid NOT NULL,
                "PlatformId" uuid NULL,
                "PlatformCode" character varying(50) NOT NULL,
                "Signature" text NULL,
                "HeadersJson" text NULL,
                "PayloadJson" text NOT NULL,
                "ReceivedAt" timestamp with time zone NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_WebhookLogs" PRIMARY KEY ("Id")
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_WebhookLogs_ReceivedAt" ON "WebhookLogs" ("ReceivedAt");
            """);
    }

    /// <summary>
    /// EnsureCreated does not alter existing databases — add the quoted-reply columns used by
    /// Messenger / Instagram message replies when they are missing.
    /// </summary>
    private static async Task EnsureMessageReplyColumnsAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Messages" ADD COLUMN IF NOT EXISTS "ReplyToMessageId" uuid NULL;
            """);
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Messages" ADD COLUMN IF NOT EXISTS "ReplyToExternalId" character varying(200) NULL;
            """);
    }

    /// <summary>
    /// Adds MenuType to Platforms / SocialAccounts and updates the unique index on accounts.
    /// </summary>
    private static async Task EnsureMenuTypeColumnsAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Platforms" ADD COLUMN IF NOT EXISTS "MenuType" character varying(50) NOT NULL DEFAULT 'integration';
            """);
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "SocialAccounts" ADD COLUMN IF NOT EXISTS "MenuType" character varying(50) NOT NULL DEFAULT 'integration';
            """);
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "Platforms" SET "MenuType" = 'integration' WHERE "MenuType" IS NULL OR TRIM("MenuType") = '';
            """);
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "SocialAccounts" SET "MenuType" = 'integration' WHERE "MenuType" IS NULL OR TRIM("MenuType") = '';
            """);
        await db.Database.ExecuteSqlRawAsync("""
            DROP INDEX IF EXISTS "IX_SocialAccounts_UserId_PlatformId";
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_SocialAccounts_UserId_PlatformId_MenuType"
            ON "SocialAccounts" ("UserId", "PlatformId", "MenuType");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            DROP INDEX IF EXISTS "IX_Platforms_Code";
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Platforms_Code_MenuType"
            ON "Platforms" ("Code", "MenuType");
            """);
    }

    private static async Task EnsureAppConnectionConfigsTableAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "AppConnectionConfigs" (
                "Id" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "PlatformId" uuid NOT NULL,
                "PlatformCode" character varying(50) NOT NULL,
                "MenuType" character varying(50) NOT NULL DEFAULT 'app_connection',
                "Label" character varying(200) NULL,
                "ClientId" character varying(200) NOT NULL,
                "ClientSecret" text NOT NULL,
                "RedirectUri" character varying(2000) NULL,
                "AuthUrl" character varying(2000) NULL,
                "BaseUrl" character varying(500) NULL,
                "Scopes" character varying(2000) NULL,
                "GraphApiVersion" character varying(20) NOT NULL DEFAULT 'v21.0',
                "WebhookVerifyToken" character varying(500) NULL,
                "PhoneNumberId" character varying(100) NULL,
                "WabaId" character varying(100) NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_AppConnectionConfigs" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_AppConnectionConfigs_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_AppConnectionConfigs_Platforms_PlatformId" FOREIGN KEY ("PlatformId") REFERENCES "Platforms" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_AppConnectionConfigs_UserId_PlatformId_MenuType"
            ON "AppConnectionConfigs" ("UserId", "PlatformId", "MenuType");
            """);
    }

    private static async Task EnsureIntegrationAppConfigsTableAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "IntegrationAppConfigs" (
                "Id" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "PlatformId" uuid NOT NULL,
                "PlatformCode" character varying(50) NOT NULL,
                "MenuType" character varying(50) NOT NULL DEFAULT 'integration',
                "Label" character varying(200) NULL,
                "ClientId" character varying(200) NOT NULL,
                "ClientSecret" text NOT NULL,
                "RedirectUri" character varying(2000) NULL,
                "AuthUrl" character varying(2000) NULL,
                "BaseUrl" character varying(500) NULL,
                "Scopes" character varying(2000) NULL,
                "GraphApiVersion" character varying(20) NOT NULL DEFAULT 'v21.0',
                "WebhookVerifyToken" character varying(500) NULL,
                "PhoneNumberId" character varying(100) NULL,
                "WabaId" character varying(100) NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_IntegrationAppConfigs" PRIMARY KEY ("Id")
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_IntegrationAppConfigs_UserId_PlatformId_MenuType"
            ON "IntegrationAppConfigs" ("UserId", "PlatformId", "MenuType");
            """);
    }

    private static async Task EnsureDeveloperAppConfigsTableAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "DeveloperAppConfigs" (
                "Id" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "PlatformId" uuid NOT NULL,
                "PlatformCode" character varying(50) NOT NULL,
                "MenuType" character varying(50) NOT NULL DEFAULT 'developer_app',
                "Label" character varying(200) NULL,
                "ClientId" character varying(200) NOT NULL,
                "ClientSecret" text NOT NULL,
                "RedirectUri" character varying(2000) NULL,
                "AuthUrl" character varying(2000) NULL,
                "BaseUrl" character varying(500) NULL,
                "Scopes" character varying(2000) NULL,
                "GraphApiVersion" character varying(20) NOT NULL DEFAULT 'v21.0',
                "WebhookVerifyToken" character varying(500) NULL,
                "PhoneNumberId" character varying(100) NULL,
                "WabaId" character varying(100) NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_DeveloperAppConfigs" PRIMARY KEY ("Id")
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_DeveloperAppConfigs_UserId_PlatformId_MenuType"
            ON "DeveloperAppConfigs" ("UserId", "PlatformId", "MenuType");
            """);
    }
}

