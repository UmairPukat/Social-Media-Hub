using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SocialMedia.Infrastructure.Persistence;

/// <summary>
/// Creates module-specific tables on databases that existed before the table split.
/// EF <see cref="DatabaseFacade.EnsureCreatedAsync"/> only runs on empty databases.
/// </summary>
internal static partial class ModuleSchemaEnsurer
{
    private static readonly string[] ModulePrefixes = ["Integration", "AppConnection", "DeveloperApp"];
    private static readonly HashSet<string> ExcludedTables = new(StringComparer.Ordinal)
    {
        "IntegrationAppConfigs",
        "AppConnectionConfigs",
        "DeveloperAppConfigs"
    };

    public static async Task EnsureAsync(AppDbContext db, ILogger? logger = null)
    {
        if (await ModuleTableMigration.TableExistsAsync(db, "IntegrationPlatforms"))
            return;

        logger?.LogInformation("Module tables missing — applying module schema to existing database...");

        var script = db.Database.GenerateCreateScript();
        await ApplyCreateTablesAsync(db, script, logger);
        await ApplyIndexesAsync(db, script, logger);

        logger?.LogInformation("Module schema applied.");
    }

    private static async Task ApplyCreateTablesAsync(AppDbContext db, string script, ILogger? logger)
    {
        foreach (Match match in CreateTableRegex().Matches(script))
        {
            var tableName = match.Groups["name"].Value;
            if (!IsModuleTable(tableName))
                continue;

            if (await ModuleTableMigration.TableExistsAsync(db, tableName))
                continue;

            var statement = match.Value.Trim();
            if (!statement.Contains("IF NOT EXISTS", StringComparison.Ordinal))
                statement = statement.Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ", StringComparison.Ordinal);

            logger?.LogInformation("Creating table {Table}...", tableName);
            await db.Database.ExecuteSqlRawAsync(statement);
        }
    }

    private static async Task ApplyIndexesAsync(AppDbContext db, string script, ILogger? logger)
    {
        foreach (var line in script.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("CREATE INDEX", StringComparison.Ordinal) &&
                !trimmed.StartsWith("CREATE UNIQUE INDEX", StringComparison.Ordinal))
                continue;

            if (!ModulePrefixes.Any(prefix => trimmed.Contains($"\"{prefix}", StringComparison.Ordinal)))
                continue;

            if (ExcludedTables.Any(t => trimmed.Contains($"\"{t}\"", StringComparison.Ordinal)))
                continue;

            var statement = trimmed.TrimEnd(';');
            if (!statement.Contains("IF NOT EXISTS", StringComparison.Ordinal))
            {
                statement = statement.StartsWith("CREATE UNIQUE INDEX", StringComparison.Ordinal)
                    ? statement.Replace("CREATE UNIQUE INDEX ", "CREATE UNIQUE INDEX IF NOT EXISTS ", StringComparison.Ordinal)
                    : statement.Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ", StringComparison.Ordinal);
            }

            try
            {
                await db.Database.ExecuteSqlRawAsync(statement);
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Index statement skipped: {Statement}", statement);
            }
        }
    }

    private static bool IsModuleTable(string tableName)
        => ModulePrefixes.Any(prefix => tableName.StartsWith(prefix, StringComparison.Ordinal))
           && !ExcludedTables.Contains(tableName);

    [GeneratedRegex(@"CREATE TABLE ""(?<name>[^""]+)"" \([\s\S]*?\);", RegexOptions.Singleline)]
    private static partial Regex CreateTableRegex();
}
