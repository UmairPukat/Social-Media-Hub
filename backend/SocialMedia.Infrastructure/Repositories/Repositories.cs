using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SocialMedia.Domain.Entities;
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

    public Task<Platform?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => DbSet.AsNoTracking().FirstOrDefaultAsync(p => p.Code == code, cancellationToken);

    public async Task<IReadOnlyList<Platform>> GetActiveAsync(CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(cancellationToken);
}

public class SocialAccountRepository : Repository<SocialAccount>, ISocialAccountRepository
{
    public SocialAccountRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<SocialAccount>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Include(a => a.Platform)
            .Include(a => a.Profiles)
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken);

    public Task<SocialAccount?> GetByUserAndPlatformAsync(Guid userId, Guid platformId, CancellationToken cancellationToken = default)
        => DbSet.Include(a => a.Auth).Include(a => a.Profiles)
            .Where(a => a.UserId == userId && a.PlatformId == platformId)
            .OrderByDescending(a => a.ConnectedAt ?? a.UpdatedAt ?? a.CreatedAt)
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

    public Task<SocialProfile?> GetByExternalProfileIdAsync(string externalProfileId, CancellationToken cancellationToken = default)
        => DbSet.Include(p => p.SocialAccount).ThenInclude(a => a!.Auth)
            .FirstOrDefaultAsync(p => p.ExternalProfileId == externalProfileId, cancellationToken);
}

public class PostRepository : Repository<Post>, IPostRepository
{
    public PostRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Post>> GetByUserProfilesAsync(Guid userId, Guid? platformId = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking()
            .Include(p => p.SocialProfile!).ThenInclude(sp => sp.SocialAccount)
            .Include(p => p.Platform)
            .Include(p => p.MediaItems)
            .Where(p => p.SocialProfile!.SocialAccount!.UserId == userId);

        if (platformId.HasValue)
            query = query.Where(p => p.PlatformId == platformId.Value);

        return await query.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
    }

    public Task<Post?> GetByExternalPostIdAsync(Guid socialProfileId, string externalPostId, CancellationToken cancellationToken = default)
        => DbSet.Include(p => p.MediaItems).FirstOrDefaultAsync(
            p => p.SocialProfileId == socialProfileId && p.ExternalPostId == externalPostId,
            cancellationToken);
}

public class CommentRepository : Repository<Comment>, ICommentRepository
{
    public CommentRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Comment>> GetByUserAsync(Guid userId, Guid? platformId = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking()
            .Include(c => c.Post).ThenInclude(p => p!.SocialProfile).ThenInclude(sp => sp!.SocialAccount).ThenInclude(a => a!.Platform)
            .Include(c => c.Post).ThenInclude(p => p!.MediaItems)
            .Include(c => c.Replies)
            .Where(c => c.Post!.SocialProfile!.SocialAccount!.UserId == userId && !c.IsDeleted);

        if (platformId.HasValue)
            query = query.Where(c => c.Post!.PlatformId == platformId.Value);

        return await query.OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken);
    }

    public Task<Comment?> GetByExternalCommentIdAsync(string externalCommentId, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(c => c.ExternalCommentId == externalCommentId, cancellationToken);
}

public class ConversationRepository : Repository<Conversation>, IConversationRepository
{
    public ConversationRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Conversation>> GetByUserAsync(Guid userId, Guid? platformId = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking()
            .Include(c => c.SocialProfile).ThenInclude(p => p!.SocialAccount).ThenInclude(a => a!.Platform)
            .Include(c => c.Messages)
            .Where(c => c.SocialProfile!.SocialAccount!.UserId == userId);

        if (platformId.HasValue)
            query = query.Where(c => c.SocialProfile!.SocialAccount!.PlatformId == platformId.Value);

        return await query.OrderByDescending(c => c.LastMessageAt).ToListAsync(cancellationToken);
    }

    public Task<Conversation?> GetByExternalConversationIdAsync(
        Guid socialProfileId,
        string externalConversationId,
        CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(
            c => c.SocialProfileId == socialProfileId && c.ExternalConversationId == externalConversationId,
            cancellationToken);
}

public class MessageRepository : Repository<Message>, IMessageRepository
{
    public MessageRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Message>> GetByUserAsync(Guid userId, Guid? platformId = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking()
            .Include(m => m.Conversation).ThenInclude(c => c!.SocialProfile).ThenInclude(p => p!.SocialAccount).ThenInclude(a => a!.Platform)
            .Where(m => m.Conversation!.SocialProfile!.SocialAccount!.UserId == userId);

        if (platformId.HasValue)
            query = query.Where(m => m.Conversation!.SocialProfile!.SocialAccount!.PlatformId == platformId.Value);

        return await query.OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);
    }

    public Task<Message?> GetByExternalMessageIdAsync(string externalMessageId, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(m => m.ExternalMessageId == externalMessageId, cancellationToken);
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
        ISyncJobRepository syncJobs)
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

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public void Dispose() => _context.Dispose();
}
