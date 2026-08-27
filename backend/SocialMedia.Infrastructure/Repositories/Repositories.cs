using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SocialMedia.Application.Catalog;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.AppConnections.Entities;
using SocialMedia.Domain.Modules.DeveloperApps.Entities;
using SocialMedia.Domain.Modules.Integrations.Entities;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Interfaces;
using SocialMedia.Infrastructure.Persistence;

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

public class PlatformRepository : Repository<Platform>, IPlatformRepository
{
    public PlatformRepository(AppDbContext context) : base(context) { }

    public Task<Platform?> GetByCodeAsync(string code, string? menuType = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(menuType))
        {
            var normalized = menuType.Trim().ToLowerInvariant();
            return DbSet.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Code == code && p.MenuType == normalized, cancellationToken);
        }

        return DbSet.AsNoTracking()
            .Where(p => p.Code == code)
            .OrderBy(p => p.MenuType == "integration" ? 0 : 1)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Platform>> GetActiveAsync(CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(cancellationToken);
}

public class SocialAccountRepository : Repository<SocialAccount>, ISocialAccountRepository
{
    public SocialAccountRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<SocialAccount>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Include(a => a.Platform)
            .Include(a => a.Auth)
            .Include(a => a.Profiles)
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken);

    public Task<SocialAccount?> GetByUserAndPlatformAsync(
        Guid userId,
        Guid platformId,
        string menuType,
        CancellationToken cancellationToken = default)
        => DbSet.Include(a => a.Auth).Include(a => a.Profiles)
            .Where(a => a.UserId == userId && a.PlatformId == platformId && a.MenuType == menuType)
            .OrderByDescending(a => a.Status == Domain.Enums.SocialAccountStatus.Connected ? 1 : 0)
            .ThenByDescending(a => a.Auth != null && a.Auth.AccessToken != null && a.Auth.AccessToken != "" ? 1 : 0)
            .ThenByDescending(a => a.Auth != null && a.Auth.RefreshToken != null && a.Auth.RefreshToken != "" ? 1 : 0)
            .ThenByDescending(a => a.ConnectedAt ?? a.UpdatedAt ?? a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<SocialAccount?> GetByExternalAccountIdAsync(string externalAccountId, CancellationToken cancellationToken = default)
        => DbSet.Include(a => a.Auth).Include(a => a.Profiles)
            .FirstOrDefaultAsync(a => a.ExternalAccountId == externalAccountId, cancellationToken);

    public Task<SocialAccount?> GetWithAuthAndProfilesAsync(Guid id, CancellationToken cancellationToken = default)
        => DbSet.Include(a => a.Auth).Include(a => a.Profiles).Include(a => a.Platform)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
}

public class SocialAuthRepository : Repository<SocialAuth>, ISocialAuthRepository
{
    public SocialAuthRepository(AppDbContext context) : base(context) { }

    public Task<SocialAuth?> GetBySocialAccountIdAsync(Guid socialAccountId, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(a => a.SocialAccountId == socialAccountId, cancellationToken);
}

public class SocialProfileRepository : Repository<SocialProfile>, ISocialProfileRepository
{
    public SocialProfileRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<SocialProfile>> GetBySocialAccountAsync(Guid socialAccountId, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking().Where(p => p.SocialAccountId == socialAccountId).ToListAsync(cancellationToken);

    public async Task<SocialProfile?> GetByExternalProfileIdAsync(string externalProfileId, string? menuType = null, CancellationToken cancellationToken = default)
    {
        var normalizedMenu = string.IsNullOrWhiteSpace(menuType) ? null : MenuTypes.Normalize(menuType);
        var query = DbSet.Include(p => p.SocialAccount).ThenInclude(a => a!.Auth)
            .Where(p => p.ExternalProfileId == externalProfileId);

        if (normalizedMenu is not null)
            query = query.Where(p => p.MenuType == normalizedMenu || p.SocialAccount!.MenuType == normalizedMenu);

        var profiles = await query.ToListAsync(cancellationToken);

        return profiles
            .OrderByDescending(p => normalizedMenu is not null && string.Equals(p.MenuType, normalizedMenu, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenByDescending(p => p.SocialAccount?.Status == Domain.Enums.SocialAccountStatus.Connected ? 1 : 0)
            .ThenByDescending(p => HasStoredOAuthTokens(p.SocialAccount?.Auth) ? 1 : 0)
            .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .FirstOrDefault();
    }

    private static bool HasStoredOAuthTokens(Domain.Entities.SocialAuth? auth)
        => auth is not null
           && (!string.IsNullOrWhiteSpace(auth.AccessToken) || !string.IsNullOrWhiteSpace(auth.RefreshToken));
}

public class PostRepository : Repository<Post>, IPostRepository
{
    public PostRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Post>> GetByUserProfilesAsync(
        Guid userId,
        Guid? platformId = null,
        string? menuType = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking()
            .Include(p => p.SocialProfile!).ThenInclude(sp => sp.SocialAccount)
            .Include(p => p.Platform)
            .Include(p => p.MediaItems)
            .Where(p => p.SocialProfile!.SocialAccount!.UserId == userId);

        if (platformId.HasValue)
            query = query.Where(p => p.PlatformId == platformId.Value);

        if (!string.IsNullOrWhiteSpace(menuType))
        {
            var normalized = MenuTypes.Normalize(menuType);
            query = query.Where(p => p.MenuType == normalized || p.SocialProfile!.SocialAccount!.MenuType == normalized);
        }

        return await query.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
    }

    public Task<Post?> GetByExternalPostIdAsync(Guid socialProfileId, string externalPostId, string? menuType = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.Include(p => p.MediaItems)
            .Where(p => p.SocialProfileId == socialProfileId && p.ExternalPostId == externalPostId);

        if (!string.IsNullOrWhiteSpace(menuType))
        {
            var normalized = MenuTypes.Normalize(menuType);
            query = query.Where(p => p.MenuType == normalized);
        }

        return query.FirstOrDefaultAsync(cancellationToken);
    }
}

public class CommentRepository : Repository<Comment>, ICommentRepository
{
    public CommentRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Comment>> GetByUserAsync(Guid userId, Guid? platformId = null, string? menuType = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking()
            .Include(c => c.Post).ThenInclude(p => p!.SocialProfile).ThenInclude(sp => sp!.SocialAccount).ThenInclude(a => a!.Platform)
            .Include(c => c.Post).ThenInclude(p => p!.MediaItems)
            .Include(c => c.Replies)
            .Where(c => c.Post!.SocialProfile!.SocialAccount!.UserId == userId && !c.IsDeleted);

        if (platformId.HasValue)
            query = query.Where(c => c.Post!.PlatformId == platformId.Value);

        if (!string.IsNullOrWhiteSpace(menuType))
        {
            var normalized = MenuTypes.Normalize(menuType);
            query = query.Where(c => c.MenuType == normalized || c.Post!.SocialProfile!.SocialAccount!.MenuType == normalized);
        }

        return await query.OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken);
    }

    public Task<Comment?> GetByExternalCommentIdAsync(string externalCommentId, string? menuType = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.Where(c => c.ExternalCommentId == externalCommentId);

        if (!string.IsNullOrWhiteSpace(menuType))
        {
            var normalized = MenuTypes.Normalize(menuType);
            query = query.Where(c => c.MenuType == normalized);
        }

        return query.FirstOrDefaultAsync(cancellationToken);
    }
}

public class ConversationRepository : Repository<Conversation>, IConversationRepository
{
    public ConversationRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Conversation>> GetByUserAsync(Guid userId, Guid? platformId = null, string? menuType = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking()
            .Include(c => c.SocialProfile).ThenInclude(p => p!.SocialAccount).ThenInclude(a => a!.Platform)
            .Include(c => c.Messages)
            .Where(c => c.SocialProfile!.SocialAccount!.UserId == userId);

        if (platformId.HasValue)
            query = query.Where(c => c.SocialProfile!.SocialAccount!.PlatformId == platformId.Value);

        if (!string.IsNullOrWhiteSpace(menuType))
        {
            var normalized = MenuTypes.Normalize(menuType);
            query = query.Where(c => c.MenuType == normalized || c.SocialProfile!.SocialAccount!.MenuType == normalized);
        }

        return await query.OrderByDescending(c => c.LastMessageAt).ToListAsync(cancellationToken);
    }

    public Task<Conversation?> GetByExternalConversationIdAsync(
        Guid socialProfileId,
        string externalConversationId,
        string? menuType = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.Where(c => c.SocialProfileId == socialProfileId && c.ExternalConversationId == externalConversationId);

        if (!string.IsNullOrWhiteSpace(menuType))
        {
            var normalized = MenuTypes.Normalize(menuType);
            query = query.Where(c => c.MenuType == normalized);
        }

        return query.FirstOrDefaultAsync(cancellationToken);
    }
}

public class MessageRepository : Repository<Message>, IMessageRepository
{
    public MessageRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Message>> GetByUserAsync(Guid userId, Guid? platformId = null, string? menuType = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking()
            .Include(m => m.Conversation).ThenInclude(c => c!.SocialProfile).ThenInclude(p => p!.SocialAccount).ThenInclude(a => a!.Platform)
            .Where(m => m.Conversation!.SocialProfile!.SocialAccount!.UserId == userId);

        if (platformId.HasValue)
            query = query.Where(m => m.Conversation!.SocialProfile!.SocialAccount!.PlatformId == platformId.Value);

        if (!string.IsNullOrWhiteSpace(menuType))
        {
            var normalized = MenuTypes.Normalize(menuType);
            query = query.Where(m => m.MenuType == normalized || m.Conversation!.SocialProfile!.SocialAccount!.MenuType == normalized);
        }

        return await query.OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);
    }

    public Task<Message?> GetByExternalMessageIdAsync(string externalMessageId, string? menuType = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.Where(m => m.ExternalMessageId == externalMessageId);

        if (!string.IsNullOrWhiteSpace(menuType))
        {
            var normalized = MenuTypes.Normalize(menuType);
            query = query.Where(m => m.MenuType == normalized);
        }

        return query.FirstOrDefaultAsync(cancellationToken);
    }
}

public class WebhookEventRepository : Repository<WebhookEvent>, IWebhookEventRepository
{
    public WebhookEventRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<WebhookEvent>> GetPendingAsync(int take = 50, CancellationToken cancellationToken = default)
        => await DbSet.Where(e => e.Status == WebhookEventStatus.Received || e.Status == WebhookEventStatus.Queued)
            .OrderBy(e => e.ReceivedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
}

public class WebhookLogRepository : Repository<WebhookLog>, IWebhookLogRepository
{
    public WebhookLogRepository(AppDbContext context) : base(context) { }
}

public class SyncJobRepository : Repository<SyncJob>, ISyncJobRepository
{
    public SyncJobRepository(AppDbContext context) : base(context) { }
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
        IPlatformRepository platforms,
        ISocialAccountRepository socialAccounts,
        ISocialAuthRepository socialAuths,
        ISocialProfileRepository socialProfiles,
        IPostRepository posts,
        ICommentRepository comments,
        IConversationRepository conversations,
        IMessageRepository messages,
        IWebhookEventRepository webhookEvents,
        IWebhookLogRepository webhookLogs,
        ISyncJobRepository syncJobs,
        IAppConnectionConfigRepository appConnectionConfigs,
        IIntegrationAppConfigRepository integrationAppConfigs,
        IDeveloperAppConfigRepository developerAppConfigs)
    {
        _context = context;
        Users = users;
        AccessTokens = accessTokens;
        Platforms = platforms;
        SocialAccounts = socialAccounts;
        SocialAuths = socialAuths;
        SocialProfiles = socialProfiles;
        Posts = posts;
        Comments = comments;
        Conversations = conversations;
        Messages = messages;
        WebhookEvents = webhookEvents;
        WebhookLogs = webhookLogs;
        SyncJobs = syncJobs;
        AppConnectionConfigs = appConnectionConfigs;
        IntegrationAppConfigs = integrationAppConfigs;
        DeveloperAppConfigs = developerAppConfigs;
    }

    public IUserRepository Users { get; }
    public IAccessTokenRepository AccessTokens { get; }
    public IPlatformRepository Platforms { get; }
    public ISocialAccountRepository SocialAccounts { get; }
    public ISocialAuthRepository SocialAuths { get; }
    public ISocialProfileRepository SocialProfiles { get; }
    public IPostRepository Posts { get; }
    public ICommentRepository Comments { get; }
    public IConversationRepository Conversations { get; }
    public IMessageRepository Messages { get; }
    public IWebhookEventRepository WebhookEvents { get; }
    public IWebhookLogRepository WebhookLogs { get; }
    public ISyncJobRepository SyncJobs { get; }
    public IAppConnectionConfigRepository AppConnectionConfigs { get; }
    public IIntegrationAppConfigRepository IntegrationAppConfigs { get; }
    public IDeveloperAppConfigRepository DeveloperAppConfigs { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public void Dispose() => _context.Dispose();
}
