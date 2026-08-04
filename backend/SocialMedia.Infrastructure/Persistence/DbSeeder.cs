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

    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        var catalogCodes = new HashSet<string>(
            PlatformCatalog.All.Select(p => p.Code),
            StringComparer.OrdinalIgnoreCase);

        foreach (var def in PlatformCatalog.All)
        {
            var existing = db.Platforms.FirstOrDefault(p => p.Code == def.Code);
            if (existing is null)
            {
                db.Platforms.Add(new Platform
                {
                    Id = def.Id,
                    Name = def.Name,
                    Code = def.Code,
                    Icon = def.Icon,
                    IsActive = true
                });
            }
            else
            {
                existing.Name = def.Name;
                existing.Icon = def.Icon;
                existing.IsActive = true;
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
}
