using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.AppConnections.Entities;
using SocialMedia.Domain.Modules.DeveloperApps.Entities;
using SocialMedia.Domain.Modules.Integrations.Entities;
using SocialMedia.Domain.Interfaces;
using SocialMedia.Infrastructure.Persistence;
using SocialMedia.Domain.Modules.SocialMediaAccounts.Entities;
using SocialMedia.Domain.Modules.AccountConnections.Entities;
using SocialMedia.Domain.Modules.AccountManagement.Entities;
using SocialMedia.Domain.Modules.AccountHub.Entities;

namespace SocialMedia.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<T> DbSet;

    public Repository(AppDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await DbSet.FindAsync([id], cancellationToken);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking().ToListAsync(cancellationToken);

    public virtual async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking().Where(predicate).ToListAsync(cancellationToken);

    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        => await DbSet.AddAsync(entity, cancellationToken);

    public virtual void Update(T entity) => DbSet.Update(entity);
    public virtual void Remove(T entity) => DbSet.Remove(entity);
    public virtual Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Context.SaveChangesAsync(cancellationToken);
}

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
}

public class AccessTokenRepository : Repository<AccessToken>, IAccessTokenRepository
{
    public AccessTokenRepository(AppDbContext context) : base(context) { }

    public Task<AccessToken?> GetValidTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return DbSet.FirstOrDefaultAsync(
            t => t.Token == token && !t.IsUsed && (t.ExpiresAt == null || t.ExpiresAt > now),
            cancellationToken);
    }
}

public class AppConnectionConfigRepository : Repository<AppConnectionConfig>, IAppConnectionConfigRepository
{
    public AppConnectionConfigRepository(AppDbContext context) : base(context) { }

    public Task<AppConnectionConfig?> GetByUserAndPlatformAsync(
        Guid userId,
        Guid platformId,
        string menuType,
        CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(
            c => c.UserId == userId && c.PlatformId == platformId && c.MenuType == menuType,
            cancellationToken);

    public async Task<IReadOnlyList<AppConnectionConfig>> GetByUserAsync(
        Guid userId,
        string menuType,
        CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Where(c => c.UserId == userId && c.MenuType == menuType)
            .ToListAsync(cancellationToken);

    public Task<AppConnectionConfig?> GetByUserAndPlatformCodeAsync(
        Guid userId,
        string platformCode,
        string menuType,
        CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(
            c => c.UserId == userId
                 && c.PlatformCode == platformCode
                 && c.MenuType == menuType,
            cancellationToken);

    public async Task<IReadOnlyList<string>> GetWebhookVerifyTokensAsync(string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Where(c => c.MenuType == menuType && c.WebhookVerifyToken != null && c.WebhookVerifyToken != "")
            .Select(c => c.WebhookVerifyToken!)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetClientSecretsAsync(string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Where(c => c.MenuType == menuType && c.ClientSecret != "")
            .Select(c => c.ClientSecret)
            .Distinct()
            .ToListAsync(cancellationToken);
}

public class IntegrationAppConfigRepository : Repository<IntegrationAppConfig>, IIntegrationAppConfigRepository
{
    public IntegrationAppConfigRepository(AppDbContext context) : base(context) { }

    public Task<IntegrationAppConfig?> GetByUserAndPlatformAsync(
        Guid userId, Guid platformId, string menuType, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(c => c.UserId == userId && c.PlatformId == platformId && c.MenuType == menuType, cancellationToken);

    public Task<IntegrationAppConfig?> GetByUserAndPlatformCodeAsync(
        Guid userId, string platformCode, string menuType, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(
            c => c.UserId == userId && c.PlatformCode == platformCode && c.MenuType == menuType,
            cancellationToken);

    public async Task<IReadOnlyList<IntegrationAppConfig>> GetByUserAsync(
        Guid userId, string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking().Where(c => c.UserId == userId && c.MenuType == menuType).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetWebhookVerifyTokensAsync(string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Where(c => c.MenuType == menuType && c.WebhookVerifyToken != null && c.WebhookVerifyToken != "")
            .Select(c => c.WebhookVerifyToken!)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetClientSecretsAsync(string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Where(c => c.MenuType == menuType && c.ClientSecret != "")
            .Select(c => c.ClientSecret)
            .Distinct()
            .ToListAsync(cancellationToken);
}

public class DeveloperAppConfigRepository : Repository<DeveloperAppConfig>, IDeveloperAppConfigRepository
{
    public DeveloperAppConfigRepository(AppDbContext context) : base(context) { }

    public Task<DeveloperAppConfig?> GetByUserAndPlatformAsync(
        Guid userId, Guid platformId, string menuType, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(c => c.UserId == userId && c.PlatformId == platformId && c.MenuType == menuType, cancellationToken);

    public Task<DeveloperAppConfig?> GetByUserAndPlatformCodeAsync(
        Guid userId, string platformCode, string menuType, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(
            c => c.UserId == userId && c.PlatformCode == platformCode && c.MenuType == menuType,
            cancellationToken);

    public async Task<IReadOnlyList<DeveloperAppConfig>> GetByUserAsync(
        Guid userId, string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking().Where(c => c.UserId == userId && c.MenuType == menuType).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetWebhookVerifyTokensAsync(string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Where(c => c.MenuType == menuType && c.WebhookVerifyToken != null && c.WebhookVerifyToken != "")
            .Select(c => c.WebhookVerifyToken!)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetClientSecretsAsync(string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Where(c => c.MenuType == menuType && c.ClientSecret != "")
            .Select(c => c.ClientSecret)
            .Distinct()
            .ToListAsync(cancellationToken);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(
        AppDbContext context,
        IUserRepository users,
        IAccessTokenRepository accessTokens,
        IAppConnectionConfigRepository appConnectionConfigs,
        IIntegrationAppConfigRepository integrationAppConfigs,
        IDeveloperAppConfigRepository developerAppConfigs,
        ISocialMediaAccountConfigRepository socialMediaAccountConfigs,
        IAccountConnectionConfigRepository accountConnectionConfigs,
        IAccountManagementConfigRepository accountManagementConfigs,
        IAccountHubConfigRepository accountHubConfigs)
    {
        _context = context;
        Users = users;
        AccessTokens = accessTokens;
        AppConnectionConfigs = appConnectionConfigs;
        IntegrationAppConfigs = integrationAppConfigs;
        DeveloperAppConfigs = developerAppConfigs;
        SocialMediaAccountConfigs = socialMediaAccountConfigs;
        AccountConnectionConfigs = accountConnectionConfigs;
        AccountManagementConfigs = accountManagementConfigs;
        AccountHubConfigs = accountHubConfigs;
    }

    public IUserRepository Users { get; }
    public IAccessTokenRepository AccessTokens { get; }
    public IAppConnectionConfigRepository AppConnectionConfigs { get; }
    public IIntegrationAppConfigRepository IntegrationAppConfigs { get; }
    public IDeveloperAppConfigRepository DeveloperAppConfigs { get; }
    public ISocialMediaAccountConfigRepository SocialMediaAccountConfigs { get; }
    public IAccountConnectionConfigRepository AccountConnectionConfigs { get; }
    public IAccountManagementConfigRepository AccountManagementConfigs { get; }
    public IAccountHubConfigRepository AccountHubConfigs { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public void Dispose() => _context.Dispose();
}

public class SocialMediaAccountConfigRepository : Repository<SocialMediaAccountConfig>, ISocialMediaAccountConfigRepository
{
    public SocialMediaAccountConfigRepository(AppDbContext context) : base(context) { }

    public Task<SocialMediaAccountConfig?> GetByUserAndPlatformAsync(
        Guid userId, Guid platformId, string menuType, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(c => c.UserId == userId && c.PlatformId == platformId && c.MenuType == menuType, cancellationToken);

    public Task<SocialMediaAccountConfig?> GetByUserAndPlatformCodeAsync(
        Guid userId, string platformCode, string menuType, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(
            c => c.UserId == userId && c.PlatformCode == platformCode && c.MenuType == menuType,
            cancellationToken);

    public async Task<IReadOnlyList<SocialMediaAccountConfig>> GetByUserAsync(
        Guid userId, string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking().Where(c => c.UserId == userId && c.MenuType == menuType).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetWebhookVerifyTokensAsync(string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Where(c => c.MenuType == menuType && c.WebhookVerifyToken != null && c.WebhookVerifyToken != "")
            .Select(c => c.WebhookVerifyToken!)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetClientSecretsAsync(string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Where(c => c.MenuType == menuType && c.ClientSecret != "")
            .Select(c => c.ClientSecret)
            .Distinct()
            .ToListAsync(cancellationToken);
}

public class AccountConnectionConfigRepository : Repository<AccountConnectionConfig>, IAccountConnectionConfigRepository
{
    public AccountConnectionConfigRepository(AppDbContext context) : base(context) { }

    public Task<AccountConnectionConfig?> GetByUserAndPlatformAsync(
        Guid userId, Guid platformId, string menuType, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(c => c.UserId == userId && c.PlatformId == platformId && c.MenuType == menuType, cancellationToken);

    public Task<AccountConnectionConfig?> GetByUserAndPlatformCodeAsync(
        Guid userId, string platformCode, string menuType, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(
            c => c.UserId == userId && c.PlatformCode == platformCode && c.MenuType == menuType,
            cancellationToken);

    public async Task<IReadOnlyList<AccountConnectionConfig>> GetByUserAsync(
        Guid userId, string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking().Where(c => c.UserId == userId && c.MenuType == menuType).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetWebhookVerifyTokensAsync(string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Where(c => c.MenuType == menuType && c.WebhookVerifyToken != null && c.WebhookVerifyToken != "")
            .Select(c => c.WebhookVerifyToken!)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetClientSecretsAsync(string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Where(c => c.MenuType == menuType && c.ClientSecret != "")
            .Select(c => c.ClientSecret)
            .Distinct()
            .ToListAsync(cancellationToken);
}
public class AccountManagementConfigRepository : Repository<AccountManagementConfig>, IAccountManagementConfigRepository
{
    public AccountManagementConfigRepository(AppDbContext context) : base(context) { }

    public Task<AccountManagementConfig?> GetByUserAndPlatformAsync(
        Guid userId, Guid platformId, string menuType, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(c => c.UserId == userId && c.PlatformId == platformId && c.MenuType == menuType, cancellationToken);

    public Task<AccountManagementConfig?> GetByUserAndPlatformCodeAsync(
        Guid userId, string platformCode, string menuType, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(
            c => c.UserId == userId && c.PlatformCode == platformCode && c.MenuType == menuType,
            cancellationToken);

    public async Task<IReadOnlyList<AccountManagementConfig>> GetByUserAsync(
        Guid userId, string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking().Where(c => c.UserId == userId && c.MenuType == menuType).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetWebhookVerifyTokensAsync(string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Where(c => c.MenuType == menuType && c.WebhookVerifyToken != null && c.WebhookVerifyToken != "")
            .Select(c => c.WebhookVerifyToken!)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetClientSecretsAsync(string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Where(c => c.MenuType == menuType && c.ClientSecret != "")
            .Select(c => c.ClientSecret)
            .Distinct()
            .ToListAsync(cancellationToken);
}
public class AccountHubConfigRepository : Repository<AccountHubConfig>, IAccountHubConfigRepository
{
    public AccountHubConfigRepository(AppDbContext context) : base(context) { }

    public Task<AccountHubConfig?> GetByUserAndPlatformAsync(
        Guid userId, Guid platformId, string menuType, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(c => c.UserId == userId && c.PlatformId == platformId && c.MenuType == menuType, cancellationToken);

    public Task<AccountHubConfig?> GetByUserAndPlatformCodeAsync(
        Guid userId, string platformCode, string menuType, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(
            c => c.UserId == userId && c.PlatformCode == platformCode && c.MenuType == menuType,
            cancellationToken);

    public async Task<IReadOnlyList<AccountHubConfig>> GetByUserAsync(
        Guid userId, string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking().Where(c => c.UserId == userId && c.MenuType == menuType).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetWebhookVerifyTokensAsync(string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Where(c => c.MenuType == menuType && c.WebhookVerifyToken != null && c.WebhookVerifyToken != "")
            .Select(c => c.WebhookVerifyToken!)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetClientSecretsAsync(string menuType, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Where(c => c.MenuType == menuType && c.ClientSecret != "")
            .Select(c => c.ClientSecret)
            .Distinct()
            .ToListAsync(cancellationToken);
}
