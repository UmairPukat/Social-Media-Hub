using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Process;
using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Modules.AppConnections.Entities;
using SocialMedia.Domain.Modules.Common.Entities;
using SocialMedia.Domain.Modules.DeveloperApps.Entities;
using SocialMedia.Domain.Modules.Integrations.Entities;
using SocialMedia.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace SocialMedia.Application.Modules.Common;

/// <summary>
/// Unified CRUD for IntegrationAppConfigs, AppConnectionConfigs, and DeveloperAppConfigs.
/// </summary>
public class ProcessAppConfigService : IProcessAppConfigService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProcessDataStoreFactory _processData;
    private readonly IConfiguration _configuration;

    public ProcessAppConfigService(
        IUnitOfWork unitOfWork,
        IProcessDataStoreFactory processData,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _processData = processData;
        _configuration = configuration;
    }

    public async Task<ApiResponse<ProcessAppConfigDto>> GetConfigAsync(
        Guid userId,
        string platformCode,
        string menuType,
        bool revealSecret = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var code = (platformCode ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedMenu = MenuTypes.Normalize(menuType);
            var dto = await LoadDtoAsync(userId, code, normalizedMenu, revealSecret, cancellationToken);
            return dto is null
                ? ApiResponse<ProcessAppConfigDto>.Fail("No app configuration found for this platform.")
                : ApiResponse<ProcessAppConfigDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            return ApiResponse<ProcessAppConfigDto>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<ProcessAppConfigDto>> SaveConfigAsync(
        Guid userId,
        SaveProcessAppConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var code = (request.PlatformCode ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(code))
                return ApiResponse<ProcessAppConfigDto>.Fail("Platform code is required.");
            if (string.IsNullOrWhiteSpace(request.ClientId))
                return ApiResponse<ProcessAppConfigDto>.Fail("Client Id is required.");

            var normalizedMenu = MenuTypes.Normalize(request.MenuType);
            var store = _processData.ForMenu(normalizedMenu);
            var platform = await store.GetPlatformByCodeAsync(code, cancellationToken);
            if (platform is null)
                return ApiResponse<ProcessAppConfigDto>.Fail($"Unknown platform '{code}' for process '{normalizedMenu}'.");

            var version = string.IsNullOrWhiteSpace(request.GraphApiVersion)
                ? "v21.0"
                : request.GraphApiVersion.Trim();

            switch (normalizedMenu)
            {
                case MenuTypes.Integration:
                    return await SaveIntegrationAsync(userId, platform, code, normalizedMenu, request, version, cancellationToken);
                case MenuTypes.AppConnection:
                    return await SaveAppConnectionAsync(userId, platform, code, normalizedMenu, request, version, cancellationToken);
                case MenuTypes.DeveloperApp:
                    return await SaveDeveloperAsync(userId, platform, code, normalizedMenu, request, version, cancellationToken);
                default:
                    return ApiResponse<ProcessAppConfigDto>.Fail("Unknown process type.");
            }
        }
        catch (Exception ex)
        {
            return ApiResponse<ProcessAppConfigDto>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<object>> DeleteConfigAsync(
        Guid userId,
        string platformCode,
        string menuType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var code = (platformCode ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedMenu = MenuTypes.Normalize(menuType);

            switch (normalizedMenu)
            {
                case MenuTypes.Integration:
                {
                    var row = await _unitOfWork.IntegrationAppConfigs.GetByUserAndPlatformCodeAsync(
                        userId, code, normalizedMenu, cancellationToken);
                    if (row is null)
                        return ApiResponse<object>.Fail("No app configuration found for this platform.");
                    _unitOfWork.IntegrationAppConfigs.Remove(row);
                    break;
                }
                case MenuTypes.AppConnection:
                {
                    var row = await _unitOfWork.AppConnectionConfigs.GetByUserAndPlatformCodeAsync(
                        userId, code, normalizedMenu, cancellationToken);
                    if (row is null)
                        return ApiResponse<object>.Fail("No app configuration found for this platform.");
                    _unitOfWork.AppConnectionConfigs.Remove(row);
                    break;
                }
                case MenuTypes.DeveloperApp:
                {
                    var row = await _unitOfWork.DeveloperAppConfigs.GetByUserAndPlatformCodeAsync(
                        userId, code, normalizedMenu, cancellationToken);
                    if (row is null)
                        return ApiResponse<object>.Fail("No app configuration found for this platform.");
                    _unitOfWork.DeveloperAppConfigs.Remove(row);
                    break;
                }
                default:
                    return ApiResponse<object>.Fail("Unknown process type.");
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<object>.Ok(null!, "App configuration deleted.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    private async Task<ProcessAppConfigDto?> LoadDtoAsync(
        Guid userId,
        string platformCode,
        string menuType,
        bool revealSecret,
        CancellationToken cancellationToken)
    {
        return menuType switch
        {
            MenuTypes.Integration => Map(await _unitOfWork.IntegrationAppConfigs.GetByUserAndPlatformCodeAsync(
                userId, platformCode, menuType, cancellationToken), revealSecret),
            MenuTypes.AppConnection => MapApp(await _unitOfWork.AppConnectionConfigs.GetByUserAndPlatformCodeAsync(
                userId, platformCode, menuType, cancellationToken), revealSecret),
            MenuTypes.DeveloperApp => MapDev(await _unitOfWork.DeveloperAppConfigs.GetByUserAndPlatformCodeAsync(
                userId, platformCode, menuType, cancellationToken), revealSecret),
            _ => null
        };
    }

    private async Task<ApiResponse<ProcessAppConfigDto>> SaveIntegrationAsync(
        Guid userId,
        PlatformEntityBase platform,
        string code,
        string menuType,
        SaveProcessAppConfigRequest request,
        string version,
        CancellationToken cancellationToken)
    {
        var config = await _unitOfWork.IntegrationAppConfigs.GetByUserAndPlatformAsync(
            userId, platform.Id, menuType, cancellationToken);
        var isNew = config is null;
        if (config is null)
        {
            if (string.IsNullOrWhiteSpace(request.ClientSecret))
                return ApiResponse<ProcessAppConfigDto>.Fail("Client Secret is required.");
            config = new IntegrationAppConfig
            {
                UserId = userId,
                PlatformId = platform.Id,
                PlatformCode = code,
                MenuType = menuType
            };
            await _unitOfWork.IntegrationAppConfigs.AddAsync(config, cancellationToken);
        }

        ApplyFields(config, request, code, menuType, version, isNew);
        if (!isNew)
            _unitOfWork.IntegrationAppConfigs.Update(config);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<ProcessAppConfigDto>.Ok(Map(config, revealSecret: false), "App configuration saved.");
    }

    private async Task<ApiResponse<ProcessAppConfigDto>> SaveAppConnectionAsync(
        Guid userId,
        PlatformEntityBase platform,
        string code,
        string menuType,
        SaveProcessAppConfigRequest request,
        string version,
        CancellationToken cancellationToken)
    {
        var config = await _unitOfWork.AppConnectionConfigs.GetByUserAndPlatformAsync(
            userId, platform.Id, menuType, cancellationToken);
        var isNew = config is null;
        if (config is null)
        {
            if (string.IsNullOrWhiteSpace(request.ClientSecret))
                return ApiResponse<ProcessAppConfigDto>.Fail("Client Secret is required.");
            config = new AppConnectionConfig
            {
                UserId = userId,
                PlatformId = platform.Id,
                PlatformCode = code,
                MenuType = menuType
            };
            await _unitOfWork.AppConnectionConfigs.AddAsync(config, cancellationToken);
        }

        ApplyFields(config, request, code, menuType, version, isNew);
        if (!isNew)
            _unitOfWork.AppConnectionConfigs.Update(config);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<ProcessAppConfigDto>.Ok(MapApp(config, revealSecret: false), "App configuration saved.");
    }

    private async Task<ApiResponse<ProcessAppConfigDto>> SaveDeveloperAsync(
        Guid userId,
        PlatformEntityBase platform,
        string code,
        string menuType,
        SaveProcessAppConfigRequest request,
        string version,
        CancellationToken cancellationToken)
    {
        var config = await _unitOfWork.DeveloperAppConfigs.GetByUserAndPlatformAsync(
            userId, platform.Id, menuType, cancellationToken);
        var isNew = config is null;
        if (config is null)
        {
            if (string.IsNullOrWhiteSpace(request.ClientSecret))
                return ApiResponse<ProcessAppConfigDto>.Fail("Client Secret is required.");
            config = new DeveloperAppConfig
            {
                UserId = userId,
                PlatformId = platform.Id,
                PlatformCode = code,
                MenuType = menuType
            };
            await _unitOfWork.DeveloperAppConfigs.AddAsync(config, cancellationToken);
        }

        ApplyFields(config, request, code, menuType, version, isNew);
        if (!isNew)
            _unitOfWork.DeveloperAppConfigs.Update(config);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<ProcessAppConfigDto>.Ok(MapDev(config, revealSecret: false), "App configuration saved.");
    }

    private void ApplyFields(IntegrationAppConfig config, SaveProcessAppConfigRequest request, string code, string menuType, string version, bool isNew)
    {
        config.Label = NullIfEmpty(request.Label);
        config.ClientId = request.ClientId.Trim();
        if (!string.IsNullOrWhiteSpace(request.ClientSecret))
            config.ClientSecret = request.ClientSecret.Trim();
        else if (isNew)
            throw new InvalidOperationException("Client Secret is required.");

        config.RedirectUri = NullIfEmpty(ResolveRedirectUri(menuType, code, request.RedirectUri));
        config.AuthUrl = ResolveStoredAuthUrl(code, NullIfEmpty(request.AuthUrl), version);
        config.BaseUrl = ResolveStoredBaseUrl(code, NullIfEmpty(request.BaseUrl));
        config.Scopes = NullIfEmpty(NormalizeStoredScopes(code, request.Scopes)) ?? DefaultScopes(code);
        config.GraphApiVersion = version;
        config.WebhookVerifyToken = NullIfEmpty(request.WebhookVerifyToken);
        config.PhoneNumberId = NullIfEmpty(request.PhoneNumberId);
        config.WabaId = NullIfEmpty(request.WabaId);
        config.UpdatedAt = DateTime.UtcNow;
    }

    private void ApplyFields(AppConnectionConfig config, SaveProcessAppConfigRequest request, string code, string menuType, string version, bool isNew)
    {
        config.Label = NullIfEmpty(request.Label);
        config.ClientId = request.ClientId.Trim();
        if (!string.IsNullOrWhiteSpace(request.ClientSecret))
            config.ClientSecret = request.ClientSecret.Trim();
        else if (isNew)
            throw new InvalidOperationException("Client Secret is required.");

        config.RedirectUri = NullIfEmpty(ResolveRedirectUri(menuType, code, request.RedirectUri));
        config.AuthUrl = ResolveStoredAuthUrl(code, NullIfEmpty(request.AuthUrl), version);
        config.BaseUrl = ResolveStoredBaseUrl(code, NullIfEmpty(request.BaseUrl));
        config.Scopes = NullIfEmpty(NormalizeStoredScopes(code, request.Scopes)) ?? DefaultScopes(code);
        config.GraphApiVersion = version;
        config.WebhookVerifyToken = NullIfEmpty(request.WebhookVerifyToken);
        config.PhoneNumberId = NullIfEmpty(request.PhoneNumberId);
        config.WabaId = NullIfEmpty(request.WabaId);
        config.UpdatedAt = DateTime.UtcNow;
    }

    private void ApplyFields(DeveloperAppConfig config, SaveProcessAppConfigRequest request, string code, string menuType, string version, bool isNew)
    {
        config.Label = NullIfEmpty(request.Label);
        config.ClientId = request.ClientId.Trim();
        if (!string.IsNullOrWhiteSpace(request.ClientSecret))
            config.ClientSecret = request.ClientSecret.Trim();
        else if (isNew)
            throw new InvalidOperationException("Client Secret is required.");

        config.RedirectUri = NullIfEmpty(ResolveRedirectUri(menuType, code, request.RedirectUri));
        config.AuthUrl = ResolveStoredAuthUrl(code, NullIfEmpty(request.AuthUrl), version);
        config.BaseUrl = ResolveStoredBaseUrl(code, NullIfEmpty(request.BaseUrl));
        config.Scopes = NullIfEmpty(NormalizeStoredScopes(code, request.Scopes)) ?? DefaultScopes(code);
        config.GraphApiVersion = version;
        config.WebhookVerifyToken = NullIfEmpty(request.WebhookVerifyToken);
        config.PhoneNumberId = NullIfEmpty(request.PhoneNumberId);
        config.WabaId = NullIfEmpty(request.WabaId);
        config.UpdatedAt = DateTime.UtcNow;
    }

    private string? ResolveRedirectUri(string menuType, string platformCode, string? configRedirectUri)
    {
        if (!ProcessOAuthRedirect.SupportsAutoRedirect(platformCode))
            return NullIfEmpty(configRedirectUri);

        var backendBase = _configuration["BackendBaseUrl"] ?? _configuration["backendBaseUrl"];
        var resolved = ProcessOAuthRedirect.Resolve(menuType, configRedirectUri, backendBase);
        return NullIfEmpty(resolved);
    }

    private string? DisplayRedirectUri(string menuType, string platformCode, string? storedRedirectUri) =>
        ResolveRedirectUri(menuType, platformCode, storedRedirectUri) ?? storedRedirectUri;

    private ProcessAppConfigDto Map(IntegrationAppConfig config, bool revealSecret) => new()
    {
        Id = config.Id,
        PlatformId = config.PlatformId,
        PlatformCode = config.PlatformCode,
        MenuType = config.MenuType,
        Label = config.Label,
        ClientId = config.ClientId,
        ClientSecret = revealSecret ? config.ClientSecret : MaskSecret(config.ClientSecret),
        HasClientSecret = !string.IsNullOrWhiteSpace(config.ClientSecret),
        RedirectUri = DisplayRedirectUri(config.MenuType, config.PlatformCode, config.RedirectUri),
        AuthUrl = config.AuthUrl,
        BaseUrl = config.BaseUrl,
        Scopes = config.Scopes,
        GraphApiVersion = config.GraphApiVersion,
        WebhookVerifyToken = config.WebhookVerifyToken,
        PhoneNumberId = config.PhoneNumberId,
        WabaId = config.WabaId,
        CreatedAt = config.CreatedAt,
        UpdatedAt = config.UpdatedAt
    };

    private ProcessAppConfigDto MapApp(AppConnectionConfig config, bool revealSecret) => new()
    {
        Id = config.Id,
        PlatformId = config.PlatformId,
        PlatformCode = config.PlatformCode,
        MenuType = config.MenuType,
        Label = config.Label,
        ClientId = config.ClientId,
        ClientSecret = revealSecret ? config.ClientSecret : MaskSecret(config.ClientSecret),
        HasClientSecret = !string.IsNullOrWhiteSpace(config.ClientSecret),
        RedirectUri = DisplayRedirectUri(config.MenuType, config.PlatformCode, config.RedirectUri),
        AuthUrl = config.AuthUrl,
        BaseUrl = config.BaseUrl,
        Scopes = config.Scopes,
        GraphApiVersion = config.GraphApiVersion,
        WebhookVerifyToken = config.WebhookVerifyToken,
        PhoneNumberId = config.PhoneNumberId,
        WabaId = config.WabaId,
        CreatedAt = config.CreatedAt,
        UpdatedAt = config.UpdatedAt
    };

    private ProcessAppConfigDto MapDev(DeveloperAppConfig config, bool revealSecret) => new()
    {
        Id = config.Id,
        PlatformId = config.PlatformId,
        PlatformCode = config.PlatformCode,
        MenuType = config.MenuType,
        Label = config.Label,
        ClientId = config.ClientId,
        ClientSecret = revealSecret ? config.ClientSecret : MaskSecret(config.ClientSecret),
        HasClientSecret = !string.IsNullOrWhiteSpace(config.ClientSecret),
        RedirectUri = DisplayRedirectUri(config.MenuType, config.PlatformCode, config.RedirectUri),
        AuthUrl = config.AuthUrl,
        BaseUrl = config.BaseUrl,
        Scopes = config.Scopes,
        GraphApiVersion = config.GraphApiVersion,
        WebhookVerifyToken = config.WebhookVerifyToken,
        PhoneNumberId = config.PhoneNumberId,
        WabaId = config.WabaId,
        CreatedAt = config.CreatedAt,
        UpdatedAt = config.UpdatedAt
    };

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string MaskSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) return string.Empty;
        if (secret.Length <= 8) return "••••••••";
        return $"{secret[..4]}…{secret[^4..]}";
    }

    private static string? NormalizeStoredScopes(string platformCode, string? scopes)
    {
        if (platformCode != "youtube")
            return NullIfEmpty(scopes);

        var normalized = PlatformCatalog.NormalizeYouTubeScopes(scopes);
        return NullIfEmpty(normalized);
    }

    private static string DefaultScopes(string platformCode) => platformCode switch
    {
        "facebook" => "public_profile,pages_show_list,pages_read_engagement,pages_read_user_content,pages_manage_engagement,pages_manage_metadata,pages_messaging,business_management",
        "instagram" => "pages_read_user_content,pages_show_list,pages_manage_metadata,pages_messaging,business_management,instagram_basic,instagram_manage_comments,instagram_manage_messages",
        "instagram_login" => "instagram_business_basic,instagram_business_manage_messages,instagram_business_manage_comments",
        "whatsapp" => "whatsapp_business_management,whatsapp_business_messaging,business_management",
        "youtube" => PlatformCatalog.DefaultScopes("youtube"),
        _ => string.Empty
    };

    private static string ResolveStoredAuthUrl(string platformCode, string? storedAuthUrl, string graphVersion)
    {
        if (platformCode == "instagram_login")
            return PlatformCatalog.DefaultAuthUrl(platformCode, graphVersion);

        return storedAuthUrl ?? PlatformCatalog.DefaultAuthUrl(platformCode, graphVersion);
    }

    private static string ResolveStoredBaseUrl(string platformCode, string? storedBaseUrl)
    {
        if (platformCode == "instagram_login")
            return PlatformCatalog.DefaultBaseUrl(platformCode);

        return storedBaseUrl ?? PlatformCatalog.DefaultBaseUrl(platformCode);
    }
}
