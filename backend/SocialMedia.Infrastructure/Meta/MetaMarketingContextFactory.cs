using Microsoft.Extensions.Options;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;
using SocialMedia.Application.Settings;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Interfaces;
using SocialMedia.Domain.Modules.Common.Entities;
using SocialMedia.Domain.Modules.SocialMediaAccounts.Entities;
using SocialMedia.Domain.Modules.AccountConnections.Entities;
using SocialMedia.Domain.Modules.AccountManagement.Entities;
using SocialMedia.Domain.Modules.AccountHub.Entities;

namespace SocialMedia.Infrastructure.Meta;

public class MetaMarketingContextFactory : IMetaMarketingContextFactory
{
    private readonly IProcessDataStoreFactory _processData;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MetaSettings _metaSettings;

    public MetaMarketingContextFactory(
        IProcessDataStoreFactory processData,
        IUnitOfWork unitOfWork,
        IOptions<MetaSettings> metaSettings)
    {
        _processData = processData;
        _unitOfWork = unitOfWork;
        _metaSettings = metaSettings.Value;
    }

    public async Task<MetaMarketingContext> CreateAsync(
        Guid userId,
        string menuType,
        CancellationToken cancellationToken = default)
    {
        if (!MenuTypes.IsKnown(menuType))
            throw new MetaGraphApiException("Unknown process menu.");

        var normalizedMenu = MenuTypes.Normalize(menuType);
        var store = _processData.ForMenu(normalizedMenu);

        var platform = await store.GetPlatformByCodeAsync("facebook", cancellationToken)
            ?? throw new MetaGraphApiException("Facebook is not configured for this module.");

        var account = await store.GetSocialAccountByUserAndPlatformAsync(userId, platform.Id, cancellationToken)
            ?? throw new MetaGraphApiException("Connect Facebook in Connect before using Meta Ads & Commerce.");

        if (account.Status != SocialAccountStatus.Connected)
            throw new MetaGraphApiException("Facebook account is not connected.");

        var auth = await store.GetSocialAuthByAccountIdAsync(account.Id, cancellationToken);
        var token = ResolveMarketingAccessToken(auth);
        if (string.IsNullOrWhiteSpace(token))
            throw new MetaGraphApiException("No Meta access token is stored. Reconnect Facebook.");

        var version = await ResolveGraphApiVersionAsync(userId, normalizedMenu, cancellationToken);
        return new MetaMarketingContext(token, version);
    }

    private async Task<string> ResolveGraphApiVersionAsync(
        Guid userId,
        string menuType,
        CancellationToken cancellationToken)
    {
        string? configuredVersion = menuType switch
        {
            MenuTypes.AppConnection => (await _unitOfWork.AppConnectionConfigs.GetByUserAndPlatformCodeAsync(
                userId, "facebook", menuType, cancellationToken))?.GraphApiVersion,
            MenuTypes.DeveloperApp => (await _unitOfWork.DeveloperAppConfigs.GetByUserAndPlatformCodeAsync(
                userId, "facebook", menuType, cancellationToken))?.GraphApiVersion,
            MenuTypes.AccountConnection => (await _unitOfWork.AccountConnectionConfigs.GetByUserAndPlatformCodeAsync(
                userId, "facebook", menuType, cancellationToken))?.GraphApiVersion,
            MenuTypes.AccountManagement => (await _unitOfWork.AccountManagementConfigs.GetByUserAndPlatformCodeAsync(
                userId, "facebook", menuType, cancellationToken))?.GraphApiVersion,
            MenuTypes.AccountHub => (await _unitOfWork.AccountHubConfigs.GetByUserAndPlatformCodeAsync(
                userId, "facebook", menuType, cancellationToken))?.GraphApiVersion,
            MenuTypes.SocialMediaAccount => (await _unitOfWork.SocialMediaAccountConfigs.GetByUserAndPlatformCodeAsync(
                userId, "facebook", menuType, cancellationToken))?.GraphApiVersion,
            MenuTypes.Integration => (await _unitOfWork.IntegrationAppConfigs.GetByUserAndPlatformCodeAsync(
                userId, "facebook", menuType, cancellationToken))?.GraphApiVersion,
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(configuredVersion))
            return configuredVersion.Trim();

        if (!string.IsNullOrWhiteSpace(_metaSettings.Facebook.GraphApiVersion))
            return _metaSettings.Facebook.GraphApiVersion.Trim();

        return "v21.0";
    }

    internal static string? ResolveMarketingAccessToken(SocialAuthEntityBase? auth)
    {
        if (auth is null)
            return null;

        if (!string.IsNullOrWhiteSpace(auth.RefreshToken))
            return auth.RefreshToken;

        return string.IsNullOrWhiteSpace(auth.AccessToken) ? null : auth.AccessToken;
    }
}
