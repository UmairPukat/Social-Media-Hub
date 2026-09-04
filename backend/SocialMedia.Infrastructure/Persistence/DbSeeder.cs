using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialMedia.Application.Catalog;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.AppConnections.Entities;
using SocialMedia.Domain.Modules.DeveloperApps.Entities;
using SocialMedia.Domain.Modules.Integrations.Entities;

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
                await SeedAsync(db, logger);
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

    /// <summary>
    /// Ensures module tables exist and legacy data is migrated. Safe to run on every startup.
    /// </summary>
    public static async Task EnsureSchemaWithRetryAsync(
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
                await EnsureSchemaAsync(db, logger);
                if (attempt > 1)
                {
                    logger?.LogInformation("Database schema ensure succeeded on attempt {Attempt}.", attempt);
                }
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger?.LogWarning(
                    ex,
                    "Database schema ensure attempt {Attempt}/{MaxAttempts} failed. Retrying in {Delay}s...",
                    attempt,
                    maxAttempts,
                    delay.TotalSeconds);

                await Task.Delay(delay);
            }
        }

        await EnsureSchemaAsync(db, logger);
    }

    public static async Task EnsureSchemaAsync(AppDbContext db, ILogger? logger = null)
    {
        await db.Database.EnsureCreatedAsync();
        await EnsureAppConnectionConfigsTableAsync(db);
        await EnsureIntegrationAppConfigsTableAsync(db);
        await EnsureDeveloperAppConfigsTableAsync(db);

        await ModuleSchemaEnsurer.EnsureAsync(db, logger);
        await ModuleTableMigration.MigrateAndDropLegacyAsync(db, logger);
        await EnsureMultiSocialAccountPerPlatformIndexAsync(db, logger);
        await SeedModulePlatformsAsync(db);
        await db.SaveChangesAsync();
    }

    public static async Task SeedAsync(AppDbContext db, ILogger? logger = null)
    {
        await EnsureSchemaAsync(db, logger);

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

    private static async Task SeedModulePlatformsAsync(AppDbContext db)
    {
        var catalogCodes = new HashSet<string>(
            PlatformCatalog.All.Select(p => p.Code),
            StringComparer.OrdinalIgnoreCase);

        foreach (var def in PlatformCatalog.All)
        {
            SeedPlatformSet(
                db.IntegrationPlatforms,
                PlatformCatalog.IdForMenu(def.Id, MenuTypes.Integration),
                def);
            SeedPlatformSet(
                db.AppConnectionPlatforms,
                PlatformCatalog.IdForMenu(def.Id, MenuTypes.AppConnection),
                def);
            SeedPlatformSet(
                db.DeveloperAppPlatforms,
                PlatformCatalog.IdForMenu(def.Id, MenuTypes.DeveloperApp),
                def);
        }

        foreach (var p in db.IntegrationPlatforms.Where(p => !catalogCodes.Contains(p.Code)))
            p.IsActive = false;
        foreach (var p in db.AppConnectionPlatforms.Where(p => !catalogCodes.Contains(p.Code)))
            p.IsActive = false;
        foreach (var p in db.DeveloperAppPlatforms.Where(p => !catalogCodes.Contains(p.Code)))
            p.IsActive = false;

        await Task.CompletedTask;
    }

    private static void SeedPlatformSet(
        DbSet<IntegrationPlatform> set,
        Guid id,
        PlatformDefinition def)
    {
        var existing = set.Local.FirstOrDefault(p => p.Id == id) ?? set.FirstOrDefault(p => p.Id == id);
        if (existing is null)
        {
            set.Add(new IntegrationPlatform { Id = id, Name = def.Name, Code = def.Code, Icon = def.Icon, IsActive = true });
            return;
        }
        existing.Name = def.Name;
        existing.Code = def.Code;
        existing.Icon = def.Icon;
        existing.IsActive = true;
    }

    private static void SeedPlatformSet(
        DbSet<AppConnectionPlatform> set,
        Guid id,
        PlatformDefinition def)
    {
        var existing = set.Local.FirstOrDefault(p => p.Id == id) ?? set.FirstOrDefault(p => p.Id == id);
        if (existing is null)
        {
            set.Add(new AppConnectionPlatform { Id = id, Name = def.Name, Code = def.Code, Icon = def.Icon, IsActive = true });
            return;
        }
        existing.Name = def.Name;
        existing.Code = def.Code;
        existing.Icon = def.Icon;
        existing.IsActive = true;
    }

    private static void SeedPlatformSet(
        DbSet<DeveloperAppPlatform> set,
        Guid id,
        PlatformDefinition def)
    {
        var existing = set.Local.FirstOrDefault(p => p.Id == id) ?? set.FirstOrDefault(p => p.Id == id);
        if (existing is null)
        {
            set.Add(new DeveloperAppPlatform { Id = id, Name = def.Name, Code = def.Code, Icon = def.Icon, IsActive = true });
            return;
        }
        existing.Name = def.Name;
        existing.Code = def.Code;
        existing.Icon = def.Icon;
        existing.IsActive = true;
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
    /// Allows multiple connected social accounts per platform (e.g. two TikTok accounts) by
    /// keying uniqueness on external account id instead of user + platform alone.
    /// </summary>
    private static async Task EnsureMultiSocialAccountPerPlatformIndexAsync(AppDbContext db, ILogger? logger = null)
    {
        foreach (var prefix in new[] { "Integration", "AppConnection", "DeveloperApp" })
        {
            var table = $"{prefix}SocialAccounts";
            if (!await ModuleTableMigration.TableExistsAsync(db, table))
                continue;

            var legacyIndex = $"IX_{table}_UserId_PlatformId";
            var nextIndex = $"IX_{table}_UserId_PlatformId_ExternalAccountId";

            logger?.LogInformation("Ensuring multi-account index on {Table}...", table);

            await db.Database.ExecuteSqlRawAsync($"""
                DROP INDEX IF EXISTS "{legacyIndex}";
                """);

            await db.Database.ExecuteSqlRawAsync($"""
                CREATE UNIQUE INDEX IF NOT EXISTS "{nextIndex}"
                ON "{table}" ("UserId", "PlatformId", "ExternalAccountId");
                """);
        }
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

    /// <summary>
    /// Adds MenuType to inbox/post entities and backfills from owning SocialAccount rows.
    /// </summary>
    private static async Task EnsureProcessDataMenuTypeColumnsAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "SocialProfiles" ADD COLUMN IF NOT EXISTS "MenuType" character varying(50) NOT NULL DEFAULT 'integration';
            """);
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Posts" ADD COLUMN IF NOT EXISTS "MenuType" character varying(50) NOT NULL DEFAULT 'integration';
            """);
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Comments" ADD COLUMN IF NOT EXISTS "MenuType" character varying(50) NOT NULL DEFAULT 'integration';
            """);
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Conversations" ADD COLUMN IF NOT EXISTS "MenuType" character varying(50) NOT NULL DEFAULT 'integration';
            """);
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Messages" ADD COLUMN IF NOT EXISTS "MenuType" character varying(50) NOT NULL DEFAULT 'integration';
            """);
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "WebhookEvents" ADD COLUMN IF NOT EXISTS "MenuType" character varying(50) NULL;
            """);

        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "SocialProfiles" sp
            SET "MenuType" = sa."MenuType"
            FROM "SocialAccounts" sa
            WHERE sp."SocialAccountId" = sa."Id"
              AND (sp."MenuType" IS NULL OR sp."MenuType" = 'integration');
            """);

        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "Posts" p
            SET "MenuType" = sp."MenuType"
            FROM "SocialProfiles" sp
            WHERE p."SocialProfileId" = sp."Id"
              AND (p."MenuType" IS NULL OR p."MenuType" = 'integration');
            """);

        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "Comments" c
            SET "MenuType" = p."MenuType"
            FROM "Posts" p
            WHERE c."PostId" = p."Id"
              AND (c."MenuType" IS NULL OR c."MenuType" = 'integration');
            """);

        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "Conversations" c
            SET "MenuType" = sp."MenuType"
            FROM "SocialProfiles" sp
            WHERE c."SocialProfileId" = sp."Id"
              AND (c."MenuType" IS NULL OR c."MenuType" = 'integration');
            """);

        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "Messages" m
            SET "MenuType" = c."MenuType"
            FROM "Conversations" c
            WHERE m."ConversationId" = c."Id"
              AND (m."MenuType" IS NULL OR m."MenuType" = 'integration');
            """);

        await db.Database.ExecuteSqlRawAsync("""
            DROP INDEX IF EXISTS "IX_SocialProfiles_ExternalProfileId";
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_SocialProfiles_ExternalProfileId_MenuType"
            ON "SocialProfiles" ("ExternalProfileId", "MenuType");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            DROP INDEX IF EXISTS "IX_Comments_ExternalCommentId";
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Comments_ExternalCommentId_MenuType"
            ON "Comments" ("ExternalCommentId", "MenuType");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            DROP INDEX IF EXISTS "IX_Conversations_SocialProfileId_ExternalConversationId";
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Conversations_SocialProfileId_ExternalConversationId_MenuType"
            ON "Conversations" ("SocialProfileId", "ExternalConversationId", "MenuType");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            DROP INDEX IF EXISTS "IX_Messages_ExternalMessageId";
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Messages_ExternalMessageId_MenuType"
            ON "Messages" ("ExternalMessageId", "MenuType");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_WebhookEvents_MenuType" ON "WebhookEvents" ("MenuType");
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

