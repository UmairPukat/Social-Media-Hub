using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Domain.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}

public interface IAccessTokenRepository : IRepository<AccessToken>
{
    Task<AccessToken?> GetValidTokenAsync(string token, CancellationToken cancellationToken = default);
}

public interface IPlatformRepository : IRepository<Platform>
{
    Task<Platform?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Platform>> GetActiveAsync(CancellationToken cancellationToken = default);
}

public interface ISocialAccountRepository : IRepository<SocialAccount>
{
    Task<IReadOnlyList<SocialAccount>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SocialAccount?> GetByUserAndPlatformAsync(Guid userId, Guid platformId, CancellationToken cancellationToken = default);
    Task<SocialAccount?> GetByUserPlatformAndAppConnectionAsync(
        Guid userId,
        Guid platformId,
        Guid appConnectionId,
        CancellationToken cancellationToken = default);
    Task<SocialAccount?> GetByExternalAccountIdAsync(string externalAccountId, CancellationToken cancellationToken = default);
    Task<SocialAccount?> GetWithAuthAndProfilesAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IMetaAppConnectionRepository : IRepository<MetaAppConnection>
{
    Task<IReadOnlyList<MetaAppConnection>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<MetaAppConnection?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}

public interface ISocialAuthRepository : IRepository<SocialAuth>
{
    Task<SocialAuth?> GetBySocialAccountIdAsync(Guid socialAccountId, CancellationToken cancellationToken = default);
}

public interface ISocialProfileRepository : IRepository<SocialProfile>
{
    Task<IReadOnlyList<SocialProfile>> GetBySocialAccountAsync(Guid socialAccountId, CancellationToken cancellationToken = default);
    Task<SocialProfile?> GetByExternalProfileIdAsync(string externalProfileId, CancellationToken cancellationToken = default);
}

public interface IPostRepository : IRepository<Post>
{
    Task<IReadOnlyList<Post>> GetByUserProfilesAsync(Guid userId, Guid? platformId = null, CancellationToken cancellationToken = default);
    Task<Post?> GetByExternalPostIdAsync(Guid socialProfileId, string externalPostId, CancellationToken cancellationToken = default);
}

public interface ICommentRepository : IRepository<Comment>
{
    Task<IReadOnlyList<Comment>> GetByUserAsync(Guid userId, Guid? platformId = null, CancellationToken cancellationToken = default);
    Task<Comment?> GetByExternalCommentIdAsync(string externalCommentId, CancellationToken cancellationToken = default);
}

public interface IConversationRepository : IRepository<Conversation>
{
    Task<IReadOnlyList<Conversation>> GetByUserAsync(Guid userId, Guid? platformId = null, CancellationToken cancellationToken = default);
    Task<Conversation?> GetByExternalConversationIdAsync(Guid socialProfileId, string externalConversationId, CancellationToken cancellationToken = default);
}

public interface IMessageRepository : IRepository<Message>
{
    Task<IReadOnlyList<Message>> GetByUserAsync(Guid userId, Guid? platformId = null, CancellationToken cancellationToken = default);
    Task<Message?> GetByExternalMessageIdAsync(string externalMessageId, CancellationToken cancellationToken = default);
}

public interface IWebhookEventRepository : IRepository<WebhookEvent>
{
    Task<IReadOnlyList<WebhookEvent>> GetPendingAsync(int take = 50, CancellationToken cancellationToken = default);
}

public interface IWebhookLogRepository : IRepository<WebhookLog>
{
}

public interface ISyncJobRepository : IRepository<SyncJob>
{
}

public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    IAccessTokenRepository AccessTokens { get; }
    IPlatformRepository Platforms { get; }
    ISocialAccountRepository SocialAccounts { get; }
    ISocialAuthRepository SocialAuths { get; }
    ISocialProfileRepository SocialProfiles { get; }
    IPostRepository Posts { get; }
    ICommentRepository Comments { get; }
    IConversationRepository Conversations { get; }
    IMessageRepository Messages { get; }
    IWebhookEventRepository WebhookEvents { get; }
    IWebhookLogRepository WebhookLogs { get; }
    ISyncJobRepository SyncJobs { get; }
    IMetaAppConnectionRepository MetaAppConnections { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
