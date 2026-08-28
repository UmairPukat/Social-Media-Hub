using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Modules.AppConnections.Entities;
using SocialMedia.Domain.Modules.DeveloperApps.Entities;
using SocialMedia.Domain.Modules.Integrations.Entities;

namespace SocialMedia.Domain.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}

public interface IAccessTokenRepository : IRepository<AccessToken>
{
    Task<AccessToken?> GetValidTokenAsync(string token, CancellationToken cancellationToken = default);
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
    IAppConnectionConfigRepository AppConnectionConfigs { get; }
    IIntegrationAppConfigRepository IntegrationAppConfigs { get; }
    IDeveloperAppConfigRepository DeveloperAppConfigs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
