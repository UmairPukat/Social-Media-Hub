using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.AppConnections.Entities;
using SocialMedia.Domain.Modules.DeveloperApps.Entities;
using SocialMedia.Domain.Modules.Integrations.Entities;
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
    Task<Platform?> GetByCodeAsync(string code, string? menuType = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Platform>> GetActiveAsync(CancellationToken cancellationToken = default);
}

public interface ISocialAccountRepository : IRepository<SocialAccount>
{
    Task<IReadOnlyList<SocialAccount>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SocialAccount?> GetByUserAndPlatformAsync(Guid userId, Guid platformId, string menuType, CancellationToken cancellationToken = default);
    Task<SocialAccount?> GetByExternalAccountIdAsync(string externalAccountId, CancellationToken cancellationToken = default);
    Task<SocialAccount?> GetWithAuthAndProfilesAsync(Guid id, CancellationToken cancellationToken = default);
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
    Task<IReadOnlyList<Post>> GetByUserProfilesAsync(
        Guid userId,
        Guid? platformId = null,
        string? menuType = null,
        CancellationToken cancellationToken = default);
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

public interface IAppConnectionConfigRepository : IRepository<AppConnectionConfig>
{
    Task<AppConnectionConfig?> GetByUserAndPlatformAsync(
        Guid userId,
        Guid platformId,
        string menuType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppConnectionConfig>> GetByUserAsync(
        Guid userId,
        string menuType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetWebhookVerifyTokensAsync(string menuType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetClientSecretsAsync(string menuType, CancellationToken cancellationToken = default);

    Task<AppConnectionConfig?> GetByUserAndPlatformCodeAsync(
        Guid userId,
        string platformCode,
        string menuType,
        CancellationToken cancellationToken = default);
}

public interface IIntegrationAppConfigRepository : IRepository<IntegrationAppConfig>
{
    Task<IntegrationAppConfig?> GetByUserAndPlatformAsync(
        Guid userId,
        Guid platformId,
        string menuType,
        CancellationToken cancellationToken = default);

    Task<IntegrationAppConfig?> GetByUserAndPlatformCodeAsync(
        Guid userId,
        string platformCode,
        string menuType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IntegrationAppConfig>> GetByUserAsync(
        Guid userId,
        string menuType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetWebhookVerifyTokensAsync(string menuType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetClientSecretsAsync(string menuType, CancellationToken cancellationToken = default);
}

public interface IDeveloperAppConfigRepository : IRepository<DeveloperAppConfig>
{
    Task<DeveloperAppConfig?> GetByUserAndPlatformAsync(
        Guid userId,
        Guid platformId,
        string menuType,
        CancellationToken cancellationToken = default);

    Task<DeveloperAppConfig?> GetByUserAndPlatformCodeAsync(
        Guid userId,
        string platformCode,
        string menuType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeveloperAppConfig>> GetByUserAsync(
        Guid userId,
        string menuType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetWebhookVerifyTokensAsync(string menuType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetClientSecretsAsync(string menuType, CancellationToken cancellationToken = default);
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
    IAppConnectionConfigRepository AppConnectionConfigs { get; }
    IIntegrationAppConfigRepository IntegrationAppConfigs { get; }
    IDeveloperAppConfigRepository DeveloperAppConfigs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
