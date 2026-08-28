using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialMedia.Infrastructure.Persistence;

namespace SocialMedia.Infrastructure.Persistence;

/// <summary>
/// Copies legacy shared-table rows into module-specific tables, then drops the legacy tables.
/// </summary>
internal static class ModuleTableMigration
{
    public static async Task MigrateAndDropLegacyAsync(AppDbContext db, ILogger? logger = null)
    {
        if (!await LegacyTableExistsAsync(db, "Platforms"))
            return;

        if (!await ModulePlatformsAlreadySeededAsync(db))
        {
            logger?.LogInformation("Migrating legacy shared tables into module-specific tables...");
            await CopyLegacyDataAsync(db);
        }

        await DropLegacyTablesAsync(db, logger);
    }

    private static async Task<bool> LegacyTableExistsAsync(AppDbContext db, string tableName)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = """
                SELECT EXISTS (
                  SELECT 1 FROM information_schema.tables
                  WHERE table_schema = 'public' AND table_name = @table
                )
                """;
            var param = cmd.CreateParameter();
            param.ParameterName = "table";
            param.Value = tableName;
            cmd.Parameters.Add(param);
            var result = await cmd.ExecuteScalarAsync();
            return result is true or 1 or (long)1;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<bool> ModulePlatformsAlreadySeededAsync(AppDbContext db)
        => await db.IntegrationPlatforms.AnyAsync()
           || await db.AppConnectionPlatforms.AnyAsync()
           || await db.DeveloperAppPlatforms.AnyAsync();

    private static async Task CopyLegacyDataAsync(AppDbContext db)
    {
        foreach (var (menu, prefix) in ModulePrefixes)
        {
            await ExecAsync(db, $"""
                INSERT INTO "{prefix}Platforms" ("Id","Name","Code","Icon","IsActive","CreatedAt","UpdatedAt")
                SELECT "Id","Name","Code","Icon","IsActive","CreatedAt","UpdatedAt"
                FROM "Platforms" WHERE "MenuType"='{menu}'
                ON CONFLICT ("Id") DO NOTHING
                """);

            await ExecAsync(db, $"""
                INSERT INTO "{prefix}SocialAccounts"
                ("Id","UserId","PlatformId","ExternalAccountId","DisplayName","Username","Email","ProfileImage",
                 "Status","ConnectedAt","LastSyncAt","MetadataJson","CreatedAt","UpdatedAt")
                SELECT "Id","UserId","PlatformId","ExternalAccountId","DisplayName","Username","Email","ProfileImage",
                       "Status","ConnectedAt","LastSyncAt","MetadataJson","CreatedAt","UpdatedAt"
                FROM "SocialAccounts" WHERE "MenuType"='{menu}'
                ON CONFLICT ("Id") DO NOTHING
                """);

            await ExecAsync(db, $"""
                INSERT INTO "{prefix}SocialAuths"
                ("Id","SocialAccountId","AccessToken","RefreshToken","ExpiresAt","Scopes","WebhookSecret","CreatedAt","UpdatedAt")
                SELECT sa."Id",sa."SocialAccountId",sa."AccessToken",sa."RefreshToken",sa."ExpiresAt",sa."Scopes",sa."WebhookSecret",sa."CreatedAt",sa."UpdatedAt"
                FROM "SocialAuths" sa
                INNER JOIN "SocialAccounts" a ON a."Id"=sa."SocialAccountId"
                WHERE a."MenuType"='{menu}'
                ON CONFLICT ("Id") DO NOTHING
                """);

            await ExecAsync(db, $"""
                INSERT INTO "{prefix}SocialProfiles"
                ("Id","SocialAccountId","ExternalProfileId","ProfileType","Name","Username","ProfileImage","MetadataJson","CreatedAt","UpdatedAt")
                SELECT sp."Id",sp."SocialAccountId",sp."ExternalProfileId",sp."ProfileType",sp."Name",sp."Username",sp."ProfileImage",sp."MetadataJson",sp."CreatedAt",sp."UpdatedAt"
                FROM "SocialProfiles" sp
                INNER JOIN "SocialAccounts" a ON a."Id"=sp."SocialAccountId"
                WHERE a."MenuType"='{menu}'
                ON CONFLICT ("Id") DO NOTHING
                """);

            await ExecAsync(db, $"""
                INSERT INTO "{prefix}Posts"
                ("Id","SocialProfileId","PlatformId","ExternalPostId","Text","Caption","Type","Status",
                 "LikeCount","CommentCount","ShareCount","ViewCount","PublishedAt","MetadataJson","ErrorMessage","CreatedAt","UpdatedAt")
                SELECT p."Id",p."SocialProfileId",p."PlatformId",p."ExternalPostId",p."Text",p."Caption",p."Type",p."Status",
                       p."LikeCount",p."CommentCount",p."ShareCount",p."ViewCount",p."PublishedAt",p."MetadataJson",p."ErrorMessage",p."CreatedAt",p."UpdatedAt"
                FROM "Posts" p
                INNER JOIN "SocialProfiles" sp ON sp."Id"=p."SocialProfileId"
                INNER JOIN "SocialAccounts" a ON a."Id"=sp."SocialAccountId"
                WHERE a."MenuType"='{menu}'
                ON CONFLICT ("Id") DO NOTHING
                """);

            await ExecAsync(db, $"""
                INSERT INTO "{prefix}Media"
                ("Id","PostId","ExternalMediaId","MediaType","Url","Thumbnail","Width","Height","Duration","DisplayOrder","CreatedAt","UpdatedAt")
                SELECT m."Id",m."PostId",m."ExternalMediaId",m."MediaType",m."Url",m."Thumbnail",m."Width",m."Height",m."Duration",m."DisplayOrder",m."CreatedAt",m."UpdatedAt"
                FROM "Media" m
                INNER JOIN "Posts" p ON p."Id"=m."PostId"
                INNER JOIN "SocialProfiles" sp ON sp."Id"=p."SocialProfileId"
                INNER JOIN "SocialAccounts" a ON a."Id"=sp."SocialAccountId"
                WHERE a."MenuType"='{menu}'
                ON CONFLICT ("Id") DO NOTHING
                """);

            await ExecAsync(db, $"""
                INSERT INTO "{prefix}Comments"
                ("Id","PostId","ParentCommentId","ExternalCommentId","AuthorId","AuthorName","AuthorImage","Message",
                 "LikeCount","IsDeleted","IsHidden","PlatformCreatedAt","CreatedAt","UpdatedAt")
                SELECT c."Id",c."PostId",c."ParentCommentId",c."ExternalCommentId",c."AuthorId",c."AuthorName",c."AuthorImage",c."Message",
                       c."LikeCount",c."IsDeleted",c."IsHidden",c."PlatformCreatedAt",c."CreatedAt",c."UpdatedAt"
                FROM "Comments" c
                INNER JOIN "Posts" p ON p."Id"=c."PostId"
                INNER JOIN "SocialProfiles" sp ON sp."Id"=p."SocialProfileId"
                INNER JOIN "SocialAccounts" a ON a."Id"=sp."SocialAccountId"
                WHERE a."MenuType"='{menu}'
                ON CONFLICT ("Id") DO NOTHING
                """);

            await ExecAsync(db, $"""
                INSERT INTO "{prefix}Conversations"
                ("Id","SocialProfileId","ExternalConversationId","CustomerId","CustomerName","CustomerImage",
                 "UnreadCount","LastMessageAt","Status","CreatedAt","UpdatedAt")
                SELECT c."Id",c."SocialProfileId",c."ExternalConversationId",c."CustomerId",c."CustomerName",c."CustomerImage",
                       c."UnreadCount",c."LastMessageAt",c."Status",c."CreatedAt",c."UpdatedAt"
                FROM "Conversations" c
                INNER JOIN "SocialProfiles" sp ON sp."Id"=c."SocialProfileId"
                INNER JOIN "SocialAccounts" a ON a."Id"=sp."SocialAccountId"
                WHERE a."MenuType"='{menu}'
                ON CONFLICT ("Id") DO NOTHING
                """);

            await ExecAsync(db, $"""
                INSERT INTO "{prefix}Messages"
                ("Id","ConversationId","ExternalMessageId","SenderId","ReceiverId","Direction","MessageType","Body",
                 "Status","PlatformCreatedAt","ReplyToMessageId","ReplyToExternalId","CreatedAt","UpdatedAt")
                SELECT m."Id",m."ConversationId",m."ExternalMessageId",m."SenderId",m."ReceiverId",m."Direction",m."MessageType",m."Body",
                       m."Status",m."PlatformCreatedAt",m."ReplyToMessageId",m."ReplyToExternalId",m."CreatedAt",m."UpdatedAt"
                FROM "Messages" m
                INNER JOIN "Conversations" c ON c."Id"=m."ConversationId"
                INNER JOIN "SocialProfiles" sp ON sp."Id"=c."SocialProfileId"
                INNER JOIN "SocialAccounts" a ON a."Id"=sp."SocialAccountId"
                WHERE a."MenuType"='{menu}'
                ON CONFLICT ("Id") DO NOTHING
                """);

            await ExecAsync(db, $"""
                INSERT INTO "{prefix}MessageAttachments"
                ("Id","MessageId","Type","Url","Thumbnail","Size","CreatedAt","UpdatedAt")
                SELECT ma."Id",ma."MessageId",ma."Type",ma."Url",ma."Thumbnail",ma."Size",ma."CreatedAt",ma."UpdatedAt"
                FROM "MessageAttachments" ma
                INNER JOIN "Messages" m ON m."Id"=ma."MessageId"
                INNER JOIN "Conversations" c ON c."Id"=m."ConversationId"
                INNER JOIN "SocialProfiles" sp ON sp."Id"=c."SocialProfileId"
                INNER JOIN "SocialAccounts" a ON a."Id"=sp."SocialAccountId"
                WHERE a."MenuType"='{menu}'
                ON CONFLICT ("Id") DO NOTHING
                """);

            await ExecAsync(db, $"""
                INSERT INTO "{prefix}WebhookEvents"
                ("Id","PlatformId","EventType","ObjectType","ExternalObjectId","HeadersJson","PayloadJson","Signature",
                 "Status","RetryCount","ReceivedAt","ProcessedAt","Error","CreatedAt","UpdatedAt")
                SELECT "Id","PlatformId","EventType","ObjectType","ExternalObjectId","HeadersJson","PayloadJson","Signature",
                       "Status","RetryCount","ReceivedAt","ProcessedAt","Error","CreatedAt","UpdatedAt"
                FROM "WebhookEvents" WHERE "MenuType"='{menu}' OR ("MenuType" IS NULL AND '{menu}'='integration')
                ON CONFLICT ("Id") DO NOTHING
                """);

            await ExecAsync(db, $"""
                INSERT INTO "{prefix}WebhookLogs"
                ("Id","PlatformId","PlatformCode","Signature","HeadersJson","PayloadJson","ReceivedAt","CreatedAt","UpdatedAt")
                SELECT wl."Id",wl."PlatformId",wl."PlatformCode",wl."Signature",wl."HeadersJson",wl."PayloadJson",wl."ReceivedAt",wl."CreatedAt",wl."UpdatedAt"
                FROM "WebhookLogs" wl
                INNER JOIN "Platforms" pl ON pl."Id"=wl."PlatformId"
                WHERE pl."MenuType"='{menu}'
                ON CONFLICT ("Id") DO NOTHING
                """);

            await ExecAsync(db, $"""
                INSERT INTO "{prefix}SyncJobs"
                ("Id","SocialAccountId","EntityType","Cursor","StartedAt","FinishedAt","Status","RecordsFetched","Error","CreatedAt","UpdatedAt")
                SELECT sj."Id",sj."SocialAccountId",sj."EntityType",sj."Cursor",sj."StartedAt",sj."FinishedAt",sj."Status",sj."RecordsFetched",sj."Error",sj."CreatedAt",sj."UpdatedAt"
                FROM "SyncJobs" sj
                INNER JOIN "SocialAccounts" a ON a."Id"=sj."SocialAccountId"
                WHERE a."MenuType"='{menu}'
                ON CONFLICT ("Id") DO NOTHING
                """);
        }
    }

    private static Task ExecAsync(AppDbContext db, string sql)
        => db.Database.ExecuteSqlRawAsync(sql);

    private static async Task DropLegacyTablesAsync(AppDbContext db, ILogger? logger)
    {
        foreach (var table in LegacyTables)
        {
            logger?.LogInformation("Dropping legacy table {Table}...", table);
            await db.Database.ExecuteSqlRawAsync($"""DROP TABLE IF EXISTS "{table}" CASCADE;""");
        }
    }

    private static readonly (string Menu, string Prefix)[] ModulePrefixes =
    [
        ("integration", "Integration"),
        ("app_connection", "AppConnection"),
        ("developer_app", "DeveloperApp")
    ];

    private static readonly string[] LegacyTables =
    [
        "MessageAttachments", "Messages", "Comments", "Media", "Posts",
        "Conversations", "SocialAuths", "SyncJobs", "SocialProfiles", "SocialAccounts",
        "WebhookEvents", "WebhookLogs", "Platforms"
    ];
}
