using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SocialMedia.Application.Auth;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Integration;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;
using SocialMedia.Application.Settings;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Interfaces;
using SocialMedia.Domain.Modules.AppConnections.Entities;
using SocialMedia.Domain.Modules.Common.Entities;
using SocialMedia.Domain.Modules.DeveloperApps.Entities;
using SocialMedia.Domain.Modules.Integrations.Entities;

namespace SocialMedia.Application.Services;

/// <summary>
/// OAuth: Meta redirects to the shared backend Callback, which exchanges the code and stores the account.
/// </summary>
public class IntegrationService : IIntegrationService
{
    private static readonly Dictionary<string, string> PlatformScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["facebook"] = "public_profile,pages_show_list,pages_read_engagement,pages_read_user_content,pages_manage_engagement,pages_manage_metadata,pages_messaging,business_management,ads_management",
        ["instagram"] = "pages_read_user_content,pages_show_list,pages_manage_metadata,pages_messaging,business_management,read_insights,pages_read_engagement,public_profile,instagram_manage_insights,instagram_basic,email,instagram_manage_comments,instagram_manage_messages",
        ["instagram_login"] = "instagram_business_basic,instagram_business_manage_messages,instagram_business_manage_comments",
        ["whatsapp"] = "whatsapp_business_management,whatsapp_business_messaging,business_management"
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly IProcessDataStoreFactory _processData;
    private readonly IFacebookService _facebookService;
    private readonly IInstagramService _instagramService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IMetaOAuthExchange _metaOAuthExchange;
    private readonly MetaSettings _meta;
    private readonly JwtSettings _jwt;
    private readonly IConfiguration _configuration;

    public IntegrationService(
        IUnitOfWork unitOfWork,
        IProcessDataStoreFactory processData,
        IFacebookService facebookService,
        IInstagramService instagramService,
        IWhatsAppService whatsAppService,
        IMetaOAuthExchange metaOAuthExchange,
        IOptions<MetaSettings> metaOptions,
        IOptions<JwtSettings> jwtOptions,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _processData = processData;
        _facebookService = facebookService;
        _instagramService = instagramService;
        _whatsAppService = whatsAppService;
        _metaOAuthExchange = metaOAuthExchange;
        _meta = metaOptions.Value;
        _jwt = jwtOptions.Value;
        _configuration = configuration;
    }

    public async Task<ApiResponse<IReadOnlyList<PlatformCardDto>>> GetPlatformCardsAsync(
        Guid userId,
        string menuType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMenu = MenuTypes.Normalize(menuType);
            var store = _processData.ForMenu(normalizedMenu);
            var platforms = (await store.GetActivePlatformsAsync(cancellationToken)).ToList();
            var accounts = await store.GetSocialAccountsByUserAsync(userId, cancellationToken);
            var byPlatform = accounts
                .Where(a => a.Status == SocialAccountStatus.Connected)
                .GroupBy(a => a.PlatformId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(AccountHasToken)
                        .ThenByDescending(a => a.ConnectedAt ?? a.UpdatedAt ?? a.CreatedAt)
                        .First());

            var configByPlatform = normalizedMenu switch
            {
                MenuTypes.AppConnection => (await _unitOfWork.AppConnectionConfigs.GetByUserAsync(userId, normalizedMenu, cancellationToken))
                    .ToDictionary(c => c.PlatformId, c => c.Id),
                MenuTypes.DeveloperApp => (await _unitOfWork.DeveloperAppConfigs.GetByUserAsync(userId, normalizedMenu, cancellationToken))
                    .ToDictionary(c => c.PlatformId, c => c.Id),
                _ => (await _unitOfWork.IntegrationAppConfigs.GetByUserAsync(userId, normalizedMenu, cancellationToken))
                    .ToDictionary(c => c.PlatformId, c => c.Id)
            };

            var cards = platforms
                .Select(p =>
                {
                    var def = PlatformCatalog.Find(p.Code);
                    byPlatform.TryGetValue(p.Id, out var account);
                    var hasAppConfig = configByPlatform.TryGetValue(p.Id, out var appConfigId);
                    return new PlatformCardDto
                    {
                        PlatformId = p.Id,
                        Code = p.Code,
                        MenuType = normalizedMenu,
                        DisplayName = def?.Name ?? p.Name,
                        Icon = def?.Icon ?? p.Icon,
                        Description = def?.Description ?? $"{p.Name} integration",
                        Category = def?.Category ?? "other",
                        CategoryLabel = def?.CategoryLabel ?? "Other",
                        SortOrder = def?.SortOrder ?? 9999,
                        CanConnect = def?.CanConnect ?? false,
                        IsConnected = account is not null && AccountHasToken(account),
                        AccountName = account?.DisplayName,
                        ConnectedAt = account?.ConnectedAt,
                        SupportsComments = def?.SupportsComments ?? false,
                        SupportsMessages = def?.SupportsMessages ?? false,
                        SupportsPosts = def?.SupportsPosts ?? false,
                        HasAppConfig = hasAppConfig,
                        AppConfigId = hasAppConfig ? appConfigId : null
                    };
                })
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.DisplayName)
                .ToList();

            return ApiResponse<IReadOnlyList<PlatformCardDto>>.Ok(cards);
        }
        catch (Exception ex)
        {
            return ApiResponse<IReadOnlyList<PlatformCardDto>>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<BeginOAuthResponse>> BeginOAuthAsync(
        Guid userId,
        BeginOAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        var platformCode = (request.PlatformCode ?? string.Empty).Trim().ToLowerInvariant();
        var menuType = MenuTypes.Normalize(request.MenuType);
        if (platformCode is not ("facebook" or "instagram" or "instagram_login" or "whatsapp"))
            return ApiResponse<BeginOAuthResponse>.Fail($"Unsupported platform '{request.PlatformCode}'.");

        string appId;
        string redirectUri;
        string version;
        string scopes;
        string authBase;

        if (menuType == MenuTypes.AppConnection)
        {
            var config = await _unitOfWork.AppConnectionConfigs.GetByUserAndPlatformCodeAsync(
                userId, platformCode, menuType, cancellationToken);
            if (config is null)
                return ApiResponse<BeginOAuthResponse>.Fail("Save this platform's app configuration before connecting.");

            appId = config.ClientId;
            redirectUri = ResolveProcessRedirectUri(platformCode, menuType, config.RedirectUri);
            version = config.GraphApiVersion;
            scopes = config.Scopes ?? PlatformScopes[platformCode];
            authBase = config.AuthUrl ?? PlatformCatalog.DefaultAuthUrl(platformCode, version);
        }
        else if (menuType == MenuTypes.DeveloperApp)
        {
            var config = await _unitOfWork.DeveloperAppConfigs.GetByUserAndPlatformCodeAsync(
                userId, platformCode, menuType, cancellationToken);
            if (config is null)
                return ApiResponse<BeginOAuthResponse>.Fail("Save this platform's app configuration before connecting.");

            appId = config.ClientId;
            redirectUri = ResolveProcessRedirectUri(platformCode, menuType, config.RedirectUri);
            version = config.GraphApiVersion;
            scopes = config.Scopes ?? PlatformScopes[platformCode];
            authBase = config.AuthUrl ?? PlatformCatalog.DefaultAuthUrl(platformCode, version);
        }
        else
        {
            var config = await _unitOfWork.IntegrationAppConfigs.GetByUserAndPlatformCodeAsync(
                userId, platformCode, menuType, cancellationToken);
            if (config is not null)
            {
                appId = config.ClientId;
                redirectUri = ResolveProcessRedirectUri(platformCode, menuType, config.RedirectUri);
                version = config.GraphApiVersion;
                scopes = config.Scopes ?? PlatformScopes[platformCode];
                authBase = config.AuthUrl ?? PlatformCatalog.DefaultAuthUrl(platformCode, version);
            }
            else
            {
                appId = ResolveAppId(platformCode);
                redirectUri = ResolveProcessRedirectUri(platformCode, menuType, null);
                version = ResolveGraphVersion(platformCode);
                scopes = PlatformScopes[platformCode];
                authBase = PlatformCatalog.DefaultAuthUrl(platformCode, version);
            }
        }

        if (string.IsNullOrWhiteSpace(appId) || appId.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
            return ApiResponse<BeginOAuthResponse>.Fail($"Meta App Id is not configured for {platformCode}.");

        if (string.IsNullOrWhiteSpace(redirectUri))
            return ApiResponse<BeginOAuthResponse>.Fail("Redirect URI is not configured. Set metaRedirectUri to the backend Callback URL.");

        var state = MetaOAuthState.Create(userId, platformCode, menuType, _jwt.SecretKey);

        var authUrl = platformCode == "instagram_login"
            ? authBase
              + $"?client_id={Uri.EscapeDataString(appId)}"
              + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
              + $"&state={Uri.EscapeDataString(state)}"
              + $"&scope={Uri.EscapeDataString(scopes)}"
              + "&response_type=code"
            : authBase
              + $"?client_id={Uri.EscapeDataString(appId)}"
              + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
              + $"&state={Uri.EscapeDataString(state)}"
              + $"&scope={Uri.EscapeDataString(scopes)}"
              + "&response_type=code";

        return ApiResponse<BeginOAuthResponse>.Ok(new BeginOAuthResponse
        {
            AuthUrl = authUrl,
            RedirectUri = redirectUri,
            PlatformCode = platformCode,
            MenuType = menuType
        });
    }

    public async Task<MetaRedirectResult> CompleteMetaRedirectAsync(
        string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken = default)
    {
        var origins = ResolveFrontendOrigins();

        if (!string.IsNullOrWhiteSpace(error))
        {
            return new MetaRedirectResult
            {
                Ok = false,
                Message = error!,
                FrontendOrigins = origins
            };
        }

        if (!MetaOAuthState.TryValidate(state, _jwt.SecretKey, out var userId, out var platformCode, out var menuType, out var stateError))
        {
            return new MetaRedirectResult
            {
                Ok = false,
                Message = stateError,
                FrontendOrigins = origins
            };
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return new MetaRedirectResult
            {
                Ok = false,
                PlatformCode = platformCode,
                MenuType = menuType,
                Message = "Missing authorization code.",
                FrontendOrigins = origins
            };
        }

        var response = await ExchangeAuthCodeAsync(userId, new OAuthCallbackRequest
        {
            PlatformCode = platformCode,
            MenuType = menuType,
            Code = code!
        }, cancellationToken);

        return new MetaRedirectResult
        {
            Ok = response.Success,
            PlatformCode = platformCode,
            MenuType = menuType,
            Message = response.Success
                ? (response.Message ?? "Connected. You can close this window.")
                : (response.Message ?? "Connection failed."),
            FrontendOrigins = origins
        };
    }

    public Task<ApiResponse<SocialAccountDto>> ExchangeAuthCodeAsync(Guid userId, OAuthCallbackRequest request, CancellationToken cancellationToken = default)
    {
        var code = (request.PlatformCode ?? string.Empty).Trim().ToLowerInvariant();
        return code is "facebook" or "instagram" or "instagram_login" or "whatsapp"
            ? HandleMetaAuthCodeAsync(userId, code, request, cancellationToken)
            : Task.FromResult(ApiResponse<SocialAccountDto>.Fail($"Unsupported platform '{request.PlatformCode}'."));
    }

    private string ResolveAppId(string platformCode) => platformCode switch
    {
        "facebook" => _meta.Facebook.AppId,
        "instagram" => string.IsNullOrWhiteSpace(_meta.Instagram.AppId) ? _meta.Facebook.AppId : _meta.Instagram.AppId,
        "instagram_login" => FirstNonEmpty(_meta.InstagramLogin.AppId, _meta.Instagram.AppId),
        "whatsapp" => _meta.WhatsApp.AppId,
        _ => string.Empty
    };

    private string ResolveGraphVersion(string platformCode) => platformCode switch
    {
        "facebook" => FirstNonEmpty(_meta.Facebook.GraphApiVersion, "v21.0"),
        "instagram" => FirstNonEmpty(_meta.Instagram.GraphApiVersion, _meta.Facebook.GraphApiVersion, "v21.0"),
        "instagram_login" => FirstNonEmpty(_meta.InstagramLogin.GraphApiVersion, _meta.Instagram.GraphApiVersion, "v21.0"),
        "whatsapp" => FirstNonEmpty(_meta.WhatsApp.GraphApiVersion, "v21.0"),
        _ => "v21.0"
    };

    private IReadOnlyList<string> ResolveFrontendOrigins()
    {
        var fromConfig = _configuration.GetSection("Cors:Origins").GetChildren()
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!);
        var fromEnv = (_configuration["corsOrigins"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var frontendBase = _configuration["frontendBaseUrl"];

        return fromConfig
            .Concat(fromEnv)
            .Append(frontendBase)
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o!.TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .DefaultIfEmpty("http://localhost:4200")
            .ToList();
    }

    private async Task<ApiResponse<SocialAccountDto>> HandleMetaAuthCodeAsync(
        Guid userId,
        string platformCode,
        OAuthCallbackRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Code))
                return ApiResponse<SocialAccountDto>.Fail("Authorization code is required.");

            var redirectUri = ResolveProcessRedirectUri(platformCode, request.MenuType, request.RedirectUri);
            if (string.IsNullOrWhiteSpace(redirectUri))
                return ApiResponse<SocialAccountDto>.Fail("Redirect URI is not configured.");

            var normalizedMenu = MenuTypes.Normalize(request.MenuType);
            OAuthTokenResult token;
            (string Id, string Name) me;

            if (normalizedMenu is MenuTypes.AppConnection or MenuTypes.DeveloperApp)
            {
                var configured = await TryExchangeWithStoredConfigAsync(
                    userId, platformCode, normalizedMenu, request, redirectUri, cancellationToken);
                if (configured is null)
                    return ApiResponse<SocialAccountDto>.Fail("Save this platform's app configuration before connecting.");
                (token, me) = configured.Value;
            }
            else if (await TryExchangeWithStoredConfigAsync(
                         userId, platformCode, normalizedMenu, request, redirectUri, cancellationToken)
                     is { } integrationConfigured)
            {
                (token, me) = integrationConfigured;
            }
            else
            {
                switch (platformCode)
                {
                    case "facebook":
                        token = await _facebookService.ExchangeCodeAsync(request.Code, redirectUri, cancellationToken);
                        me = await _facebookService.GetMeAsync(token.AccessToken, cancellationToken);
                        break;
                    case "instagram":
                        token = await _instagramService.ExchangeCodeAsync(request.Code, redirectUri, cancellationToken);
                        me = await _instagramService.GetMeAsync(token.AccessToken, cancellationToken);
                        break;
                    case "instagram_login":
                        token = await _instagramService.ExchangeInstagramLoginCodeAsync(request.Code, redirectUri, cancellationToken);
                        me = await _instagramService.GetInstagramLoginMeAsync(token.AccessToken, cancellationToken);
                        break;
                    case "whatsapp":
                        token = await _whatsAppService.ExchangeCodeAsync(request.Code, redirectUri, cancellationToken);
                        me = await _whatsAppService.GetMeAsync(token.AccessToken, cancellationToken);
                        break;
                    default:
                        return ApiResponse<SocialAccountDto>.Fail($"Unsupported platform '{platformCode}'.");
                }
            }

            return await PersistConnectedAccountAsync(
                userId,
                platformCode,
                normalizedMenu,
                token.AccessToken,
                token.ExpiresAt,
                me.Id,
                me.Name,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return ApiResponse<SocialAccountDto>.Fail(ex.Message);
        }
    }

    private string ResolveProcessRedirectUri(string platformCode, string? menuType, string? configRedirectUri)
    {
        if (!string.IsNullOrWhiteSpace(configRedirectUri))
            return configRedirectUri.Trim();

        var normalizedMenu = MenuTypes.Normalize(menuType);
        var backendBase = FirstNonEmpty(
            _configuration["BackendBaseUrl"],
            _configuration["backendBaseUrl"],
            ExtractBackendBaseFromMetaRedirect());

        if (!string.IsNullOrWhiteSpace(backendBase))
            return $"{backendBase.TrimEnd('/')}{ProcessModules.CallbackRouteFor(normalizedMenu)}";

        if (normalizedMenu == MenuTypes.Integration)
            return ResolveRedirectUri(platformCode, null);

        return string.Empty;
    }

    private string? ExtractBackendBaseFromMetaRedirect()
    {
        var shared = FirstNonEmpty(
            _meta.Facebook.RedirectUri,
            _meta.Instagram.RedirectUri,
            _meta.InstagramLogin.RedirectUri,
            _meta.WhatsApp.RedirectUri);
        if (string.IsNullOrWhiteSpace(shared) || !Uri.TryCreate(shared, UriKind.Absolute, out var uri))
            return null;

        return $"{uri.Scheme}://{uri.Authority}";
    }

    private async Task<(OAuthTokenResult Token, (string Id, string Name) Me)?> TryExchangeWithStoredConfigAsync(
        Guid userId,
        string platformCode,
        string menuType,
        OAuthCallbackRequest request,
        string fallbackRedirectUri,
        CancellationToken cancellationToken)
    {
        string? clientId = null;
        string? clientSecret = null;
        string? redirect = null;
        string? version = null;
        string? baseUrl = null;

        if (menuType == MenuTypes.AppConnection)
        {
            var config = await _unitOfWork.AppConnectionConfigs.GetByUserAndPlatformCodeAsync(
                userId, platformCode, menuType, cancellationToken);
            if (config is null) return null;
            clientId = config.ClientId;
            clientSecret = config.ClientSecret;
            redirect = config.RedirectUri ?? fallbackRedirectUri;
            version = config.GraphApiVersion;
            baseUrl = config.BaseUrl;
        }
        else if (menuType == MenuTypes.DeveloperApp)
        {
            var config = await _unitOfWork.DeveloperAppConfigs.GetByUserAndPlatformCodeAsync(
                userId, platformCode, menuType, cancellationToken);
            if (config is null) return null;
            clientId = config.ClientId;
            clientSecret = config.ClientSecret;
            redirect = config.RedirectUri ?? fallbackRedirectUri;
            version = config.GraphApiVersion;
            baseUrl = config.BaseUrl;
        }
        else
        {
            var config = await _unitOfWork.IntegrationAppConfigs.GetByUserAndPlatformCodeAsync(
                userId, platformCode, menuType, cancellationToken);
            if (config is null) return null;
            clientId = config.ClientId;
            clientSecret = config.ClientSecret;
            redirect = config.RedirectUri ?? fallbackRedirectUri;
            version = config.GraphApiVersion;
            baseUrl = config.BaseUrl;
        }

        var token = await _metaOAuthExchange.ExchangeAuthorizationCodeAsync(
            new MetaOAuthCredentials(
                platformCode,
                request.Code,
                redirect!,
                clientId!,
                clientSecret!,
                version!,
                baseUrl),
            cancellationToken);

        var me = platformCode switch
        {
            "facebook" => await _facebookService.GetMeAsync(token.AccessToken, cancellationToken),
            "instagram" => await _instagramService.GetMeAsync(token.AccessToken, cancellationToken),
            "instagram_login" => await _instagramService.GetInstagramLoginMeAsync(token.AccessToken, cancellationToken),
            "whatsapp" => await _whatsAppService.GetMeAsync(token.AccessToken, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported platform '{platformCode}'.")
        };

        return (token, me);
    }

    private string ResolveRedirectUri(string platformCode, string? fromRequest)
    {
        if (!string.IsNullOrWhiteSpace(fromRequest))
            return fromRequest!;

        // Prefer a single shared callback URI; fall back to the first configured value.
        var shared = FirstNonEmpty(
            _meta.Facebook.RedirectUri,
            _meta.Instagram.RedirectUri,
            _meta.InstagramLogin.RedirectUri,
            _meta.WhatsApp.RedirectUri);

        return platformCode switch
        {
            "facebook" => FirstNonEmpty(_meta.Facebook.RedirectUri, shared),
            "instagram" => FirstNonEmpty(_meta.Instagram.RedirectUri, _meta.Facebook.RedirectUri, shared),
            "instagram_login" => FirstNonEmpty(_meta.InstagramLogin.RedirectUri, _meta.Instagram.RedirectUri, shared),
            "whatsapp" => FirstNonEmpty(_meta.WhatsApp.RedirectUri, shared),
            _ => string.Empty
        };
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;

    private async Task<(string? PhoneNumberId, string? WabaId)> ResolveWhatsAppRoutingIdsAsync(
        Guid userId,
        string platformCode,
        string menuType,
        CancellationToken cancellationToken)
    {
        if (menuType == MenuTypes.AppConnection)
        {
            var config = await _unitOfWork.AppConnectionConfigs.GetByUserAndPlatformCodeAsync(
                userId, platformCode, menuType, cancellationToken);
            if (config is not null)
                return (config.PhoneNumberId, config.WabaId);
        }
        else if (menuType == MenuTypes.DeveloperApp)
        {
            var config = await _unitOfWork.DeveloperAppConfigs.GetByUserAndPlatformCodeAsync(
                userId, platformCode, menuType, cancellationToken);
            if (config is not null)
                return (config.PhoneNumberId, config.WabaId);
        }
        else
        {
            var config = await _unitOfWork.IntegrationAppConfigs.GetByUserAndPlatformCodeAsync(
                userId, platformCode, menuType, cancellationToken);
            if (config is not null)
                return (config.PhoneNumberId, config.WabaId);
        }

        return (_meta.WhatsApp.PhoneNumberId, _meta.WhatsApp.WabaId);
    }

    private async Task<ApiResponse<SocialAccountDto>> PersistConnectedAccountAsync(
        Guid userId,
        string platformCode,
        string menuType,
        string accessToken,
        DateTime? expiresAt,
        string externalAccountId,
        string displayName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return ApiResponse<SocialAccountDto>.Fail("Meta did not return an access token. Try connecting again.");

        var normalizedMenu = MenuTypes.Normalize(menuType);
        var store = _processData.ForMenu(normalizedMenu);
        var platform = await store.GetPlatformByCodeAsync(platformCode, cancellationToken);
        if (platform is null)
            return ApiResponse<SocialAccountDto>.Fail($"Unknown platform '{platformCode}'.");

        var account = await store.GetSocialAccountByUserAndPlatformAsync(userId, platform.Id, cancellationToken);
        var isNewAccount = account is null;
        if (account is null)
        {
            account = store.NewSocialAccount();
            account.UserId = userId;
            account.PlatformId = platform.Id;
            await store.AddSocialAccountAsync(account, cancellationToken);
        }

        account.ExternalAccountId = externalAccountId;
        account.DisplayName = displayName;
        account.Status = SocialAccountStatus.Connected;
        account.ConnectedAt = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;
        if (!isNewAccount)
            store.UpdateSocialAccount(account);

        await store.SaveChangesAsync(cancellationToken);

        var auth = await store.GetSocialAuthByAccountIdAsync(account.Id, cancellationToken);
        var isNewAuth = auth is null;
        if (auth is null)
        {
            auth = store.NewSocialAuth();
            auth.SocialAccountId = account.Id;
            await store.AddSocialAuthAsync(auth, cancellationToken);
        }

        auth.AccessToken = accessToken;
        auth.RefreshToken = accessToken;
        auth.ExpiresAt = expiresAt;
        auth.UpdatedAt = DateTime.UtcNow;
        if (!isNewAuth)
            store.UpdateSocialAuth(auth);

        await ConsolidateLegacyDuplicateAccountsAsync(store, userId, platform.Id, account.Id, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);

        var persistedAuth = await store.GetSocialAuthByAccountIdAsync(account.Id, cancellationToken);
        if (persistedAuth is null || string.IsNullOrWhiteSpace(persistedAuth.AccessToken))
            return ApiResponse<SocialAccountDto>.Fail("The access token could not be saved. Disconnect and connect again.");

        var requiresPageSelection = SupportsPageSelection(platformCode);
        if (!requiresPageSelection)
        {
            var whatsAppIds = await ResolveWhatsAppRoutingIdsAsync(userId, platformCode, normalizedMenu, cancellationToken);
            IReadOnlyList<SocialProfileDraft> drafts = platformCode switch
            {
                "instagram_login" => await _instagramService.DiscoverInstagramLoginProfilesAsync(accessToken, cancellationToken),
                "whatsapp" => await _whatsAppService.DiscoverProfilesAsync(
                    accessToken, whatsAppIds.PhoneNumberId, whatsAppIds.WabaId, cancellationToken),
                _ => Array.Empty<SocialProfileDraft>()
            };

            foreach (var draft in drafts)
                await UpsertProfileAsync(store, account, draft, cancellationToken);

            await QueueInitialSyncAsync(store, account, cancellationToken);
            await store.SaveChangesAsync(cancellationToken);
        }

        var reloaded = await store.GetSocialAccountWithAuthAndProfilesAsync(account.Id, cancellationToken);
        var dto = MapAccount(reloaded ?? account, platform, normalizedMenu);
        dto.RequiresPageSelection = requiresPageSelection;

        return ApiResponse<SocialAccountDto>.Ok(dto, requiresPageSelection
            ? "Signed in with Meta. Choose the page you want to manage."
            : "Account connected.");
    }

    public async Task<ApiResponse<IReadOnlyList<MetaPageDto>>> GetPagesAsync(
        Guid userId,
        string platformCode,
        string menuType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var code = (platformCode ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedMenu = MenuTypes.Normalize(menuType);
            var store = _processData.ForMenu(normalizedMenu);
            if (!SupportsPageSelection(code))
                return ApiResponse<IReadOnlyList<MetaPageDto>>.Fail($"Page selection is not available for '{platformCode}'.");

            var platform = await store.GetPlatformByCodeAsync(code, cancellationToken);
            if (platform is null)
                return ApiResponse<IReadOnlyList<MetaPageDto>>.Fail("Unknown platform.");

            var account = await store.GetSocialAccountByUserAndPlatformAsync(userId, platform.Id, cancellationToken);
            var userToken = ResolveUserAccessToken(account);
            if (string.IsNullOrWhiteSpace(userToken))
                return ApiResponse<IReadOnlyList<MetaPageDto>>.Fail("Sign in with Meta again — no stored login token was found.");

            var pages = await ListPagesAsync(code, userToken!, cancellationToken);
            var connectedPageIds = ResolveConnectedPageIds(account, code);
            var data = pages
                .Select(p => MapPage(p, code, connectedPageIds))
                .OrderByDescending(p => p.IsSelected)
                .ThenByDescending(p => p.IsEligible)
                .ThenBy(p => p.PageName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            return ApiResponse<IReadOnlyList<MetaPageDto>>.Ok(data);
        }
        catch (Exception ex)
        {
            return ApiResponse<IReadOnlyList<MetaPageDto>>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<SocialAccountDto>> SelectPageAsync(
        Guid userId,
        SelectPageRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var code = (request.PlatformCode ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedMenu = MenuTypes.Normalize(request.MenuType);
            var store = _processData.ForMenu(normalizedMenu);
            if (!SupportsPageSelection(code))
                return ApiResponse<SocialAccountDto>.Fail($"Page selection is not available for '{request.PlatformCode}'.");
            if (string.IsNullOrWhiteSpace(request.PageId))
                return ApiResponse<SocialAccountDto>.Fail("Select a page first.");

            var platform = await store.GetPlatformByCodeAsync(code, cancellationToken);
            if (platform is null)
                return ApiResponse<SocialAccountDto>.Fail("Unknown platform.");

            var account = await store.GetSocialAccountByUserAndPlatformAsync(userId, platform.Id, cancellationToken);
            var auth = account is null ? null : ProcessEntityNav.Auth(account)
                ?? await store.GetSocialAuthByAccountIdAsync(account.Id, cancellationToken);
            var userToken = ResolveUserAccessToken(account);
            if (account is null || auth is null || string.IsNullOrWhiteSpace(userToken))
                return ApiResponse<SocialAccountDto>.Fail("Sign in with Meta before selecting a page.");

            var pages = await ListPagesAsync(code, userToken!, cancellationToken);
            var page = pages.FirstOrDefault(p => p.PageId == request.PageId);
            if (page is null)
                return ApiResponse<SocialAccountDto>.Fail("That page is no longer granted to this Meta login. Reconnect and try again.");

            if (code == "instagram" && string.IsNullOrWhiteSpace(page.InstagramId))
                return ApiResponse<SocialAccountDto>.Fail($"'{page.PageName}' has no Instagram Business account linked to it.");

            var draft = code == "instagram"
                ? new SocialProfileDraft
                {
                    ExternalProfileId = page.InstagramId!,
                    Name = page.InstagramName ?? page.InstagramUsername ?? page.PageName,
                    Username = page.InstagramUsername,
                    ProfileImage = page.InstagramImage,
                    ProfileType = "InstagramBusiness",
                    PageId = page.PageId,
                    PageAccessToken = page.PageAccessToken
                }
                : new SocialProfileDraft
                {
                    ExternalProfileId = page.PageId,
                    Name = page.PageName,
                    ProfileImage = page.PageImage,
                    ProfileType = "FacebookPage",
                    PageId = page.PageId,
                    PageAccessToken = page.PageAccessToken
                };

            await UpsertProfileAsync(store, account, draft, cancellationToken);

            if (!string.IsNullOrWhiteSpace(page.PageAccessToken) && auth is not null)
            {
                auth.AccessToken = page.PageAccessToken!;
                auth.UpdatedAt = DateTime.UtcNow;
                store.UpdateSocialAuth(auth);
            }

            account.Status = SocialAccountStatus.Connected;
            account.ConnectedAt ??= DateTime.UtcNow;
            account.MetadataJson = JsonSerializer.Serialize(new
            {
                selectedPageId = page.PageId,
                selectedPageName = page.PageName
            });
            store.UpdateSocialAccount(account);
            await QueueInitialSyncAsync(store, account, cancellationToken);

            await store.SaveChangesAsync(cancellationToken);

            var subscribeWarning = await SubscribePageWebhooksAsync(code, page, cancellationToken);

            var reloaded = await store.GetSocialAccountWithAuthAndProfilesAsync(account.Id, cancellationToken);
            var message = string.IsNullOrWhiteSpace(subscribeWarning)
                ? $"{page.PageName} connected."
                : $"{page.PageName} connected, but webhook subscription failed: {subscribeWarning}";

            return ApiResponse<SocialAccountDto>.Ok(MapAccount(reloaded ?? account, platform, normalizedMenu), message);
        }
        catch (Exception ex)
        {
            return ApiResponse<SocialAccountDto>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<ConnectionDetailsDto>> GetConnectionDetailsAsync(
        Guid userId,
        string platformCode,
        string menuType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var code = (platformCode ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedMenu = MenuTypes.Normalize(menuType);
            var store = _processData.ForMenu(normalizedMenu);
            var platform = await store.GetPlatformByCodeAsync(code, cancellationToken);
            if (platform is null)
                return ApiResponse<ConnectionDetailsDto>.Fail("Unknown platform.");

            var account = await store.GetSocialAccountByUserAndPlatformAsync(userId, platform.Id, cancellationToken);
            if (account is null || account.Status != SocialAccountStatus.Connected)
                return ApiResponse<ConnectionDetailsDto>.Fail($"{platform.Name} is not connected.");

            var auth = ProcessEntityNav.Auth(account)
                ?? await store.GetSocialAuthByAccountIdAsync(account.Id, cancellationToken);
            var effectiveToken = ResolveUserAccessToken(auth);

            var profiles = ProcessEntityNav.Profiles(account);
            var profile = profiles.FirstOrDefault();
            var pageId = ResolveSelectedPageId(account, code);
            var isInstagram = code is "instagram" or "instagram_login";

            var details = new ConnectionDetailsDto
            {
                PlatformCode = platform.Code,
                MenuType = normalizedMenu,
                PlatformName = platform.Name,
                AccountName = account.DisplayName,
                Status = account.Status,
                ConnectedAt = account.ConnectedAt,
                LastSyncAt = account.LastSyncAt,
                PageId = code == "instagram_login" ? null : pageId,
                PageName = code == "instagram_login"
                    ? (profile?.Name ?? account.DisplayName)
                    : (ReadJsonString(account.MetadataJson, "selectedPageName") ?? profile?.Name),
                PageImage = profile?.ProfileImage,
                InstagramId = isInstagram ? profile?.ExternalProfileId : null,
                InstagramUsername = isInstagram ? profile?.Username : null,
                AccessToken = string.IsNullOrWhiteSpace(effectiveToken) ? null : effectiveToken,
                Profiles = profiles.Select(p => new SocialProfileDto
                {
                    Id = p.Id,
                    ExternalProfileId = p.ExternalProfileId,
                    ProfileType = p.ProfileType.ToString(),
                    Name = p.Name,
                    Username = p.Username
                }).ToList()
            };

            if (string.IsNullOrWhiteSpace(effectiveToken))
            {
                details.WebhookError =
                    "No access token is stored for this connection. Disconnect Instagram Login, then connect again to refresh the token.";
            }
            else
            {
                await ApplyWebhookStatusAsync(details, code, pageId, effectiveToken, cancellationToken);
            }
            return ApiResponse<ConnectionDetailsDto>.Ok(details);
        }
        catch (Exception ex)
        {
            return ApiResponse<ConnectionDetailsDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Reads the page's live subscription so the popup shows whether webhooks really arrive.
    /// A failure here is reported in the DTO rather than failing the whole request.
    /// </summary>
    private async Task ApplyWebhookStatusAsync(
        ConnectionDetailsDto details,
        string platformCode,
        string? pageId,
        string? pageAccessToken,
        CancellationToken cancellationToken)
    {
        if (!SupportsPageSelection(platformCode))
        {
            if (platformCode == "instagram_login")
            {
                details.WebhookError =
                    "Instagram Login webhooks are configured in the Meta App Dashboard (comments, messages), not via a Facebook Page subscription.";
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(pageId) || string.IsNullOrWhiteSpace(pageAccessToken))
        {
            details.WebhookError = "No page is selected yet, so no webhook subscription exists.";
            return;
        }

        try
        {
            var fields = platformCode == "instagram"
                ? await _instagramService.GetSubscribedFieldsAsync(pageId!, pageAccessToken!, cancellationToken)
                : await _facebookService.GetSubscribedFieldsAsync(pageId!, pageAccessToken!, cancellationToken);

            details.SubscribedFields = fields;
            details.WebhookSubscribed = fields.Count > 0;
            if (fields.Count == 0)
                details.WebhookError = "The page is not subscribed to any webhook fields yet.";
        }
        catch (Exception ex)
        {
            details.WebhookError = ex.Message;
        }
    }

    /// <summary>
    /// Subscribes the picked page to webhook fields. Returns null on success, or the reason to
    /// surface — a failed subscription must not undo an otherwise successful connection.
    /// </summary>
    private async Task<string?> SubscribePageWebhooksAsync(
        string platformCode,
        MetaPageInfo page,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(page.PageAccessToken))
            return "no page access token was returned by Meta.";

        try
        {
            if (platformCode == "instagram")
                await _instagramService.SubscribePageWebhooksAsync(page.PageId, page.PageAccessToken!, cancellationToken);
            else
                await _facebookService.SubscribePageWebhooksAsync(page.PageId, page.PageAccessToken!, cancellationToken);

            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Best-effort unsubscribe so a disconnected page stops sending webhooks.</summary>
    private async Task UnsubscribePageWebhooksAsync(
        string platformCode,
        SocialAccountEntityBase account,
        string? pageAccessToken,
        CancellationToken cancellationToken)
    {
        var pageId = ResolveSelectedPageId(account, platformCode);
        if (string.IsNullOrWhiteSpace(pageId) || string.IsNullOrWhiteSpace(pageAccessToken))
            return;

        try
        {
            if (platformCode == "instagram")
                await _instagramService.UnsubscribePageWebhooksAsync(pageId!, pageAccessToken!, cancellationToken);
            else
                await _facebookService.UnsubscribePageWebhooksAsync(pageId!, pageAccessToken!, cancellationToken);
        }
        catch
        {
            // The local account still disconnects; the page subscription can be removed in Meta.
        }
    }

    private static string? ResolveSelectedPageId(SocialAccountEntityBase account, string platformCode)
    {
        var selected = ReadJsonString(account.MetadataJson, "selectedPageId");
        if (!string.IsNullOrWhiteSpace(selected))
            return selected;

        foreach (var profile in ProcessEntityNav.Profiles(account))
        {
            var pageId = ReadJsonString(profile.MetadataJson, "pageId");
            if (!string.IsNullOrWhiteSpace(pageId))
                return pageId;

            if (platformCode == "facebook" && !string.IsNullOrWhiteSpace(profile.ExternalProfileId))
                return profile.ExternalProfileId;
        }

        return null;
    }

    private async Task<IReadOnlyList<MetaPageInfo>> ListPagesAsync(
        string platformCode,
        string userAccessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            return platformCode == "instagram"
                ? await _instagramService.ListPagesAsync(userAccessToken, cancellationToken)
                : await _facebookService.ListPagesAsync(userAccessToken, cancellationToken);
        }
        catch (Exception ex)
        {
            // A stale token, or one stored before page selection existed, cannot list pages.
            throw new InvalidOperationException(
                "Could not read your Facebook Pages with the stored Meta login. Reconnect with Meta and try again.", ex);
        }
    }

    private static bool AccountHasToken(SocialAccountEntityBase account)
    {
        var auth = ProcessEntityNav.Auth(account);
        return auth is not null
               && (!string.IsNullOrWhiteSpace(auth.AccessToken)
                   || !string.IsNullOrWhiteSpace(auth.RefreshToken));
    }

    private static bool SupportsPageSelection(string platformCode) =>
        platformCode.Equals("facebook", StringComparison.OrdinalIgnoreCase) ||
        platformCode.Equals("instagram", StringComparison.OrdinalIgnoreCase);

    private async Task<SocialProfileEntityBase> UpsertProfileAsync(
        IProcessDataStore store,
        SocialAccountEntityBase account,
        SocialProfileDraft draft,
        CancellationToken cancellationToken)
    {
        var profiles = await store.GetProfilesByAccountAsync(account.Id, cancellationToken);
        var existingId = profiles.FirstOrDefault(p => p.ExternalProfileId == draft.ExternalProfileId)?.Id;

        var profile = existingId.HasValue
            ? await store.GetProfileByIdAsync(existingId.Value, cancellationToken)
            : null;

        var isNew = profile is null;
        if (profile is null)
        {
            profile = store.NewSocialProfile();
            profile.SocialAccountId = account.Id;
            await store.AddSocialProfileAsync(profile, cancellationToken);
        }

        profile.SocialAccountId = account.Id;
        profile.ExternalProfileId = draft.ExternalProfileId;
        profile.Name = draft.Name;
        profile.Username = draft.Username;
        profile.ProfileImage = draft.ProfileImage;
        profile.ProfileType = ParseProfileType(draft.ProfileType);

        var metadata = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(draft.PageId))
            metadata["pageId"] = draft.PageId!;
        if (draft.AlternateExternalIds.Count > 0)
            metadata["alternateIds"] = draft.AlternateExternalIds;
        if (metadata.Count > 0)
            profile.MetadataJson = JsonSerializer.Serialize(metadata);

        profile.UpdatedAt = DateTime.UtcNow;
        if (!isNew)
            store.UpdateSocialProfile(profile);

        await ReassignOrphanProfilesAsync(store, account, profile, cancellationToken);

        return profile;
    }

    private async Task ReassignOrphanProfilesAsync(
        IProcessDataStore store,
        SocialAccountEntityBase account,
        SocialProfileEntityBase canonicalProfile,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(canonicalProfile.ExternalProfileId))
            return;

        var platform = await store.GetPlatformByIdAsync(account.PlatformId, cancellationToken);
        var platformCode = platform?.Code;
        if (string.IsNullOrWhiteSpace(platformCode))
            return;

        var orphans = await store.FindProfilesByExternalIdAsync(canonicalProfile.ExternalProfileId, cancellationToken);

        foreach (var orphanSnapshot in orphans)
        {
            if (orphanSnapshot.Id == canonicalProfile.Id)
                continue;

            var owner = await store.GetSocialAccountByIdAsync(orphanSnapshot.SocialAccountId, cancellationToken);
            if (owner is null || owner.UserId != account.UserId)
                continue;

            var ownerPlatform = await store.GetPlatformByIdAsync(owner.PlatformId, cancellationToken);
            if (!string.Equals(ownerPlatform?.Code, platformCode, StringComparison.OrdinalIgnoreCase))
                continue;

            var ownerAuth = await store.GetSocialAuthByAccountIdAsync(owner.Id, cancellationToken);
            if (owner.Status == SocialAccountStatus.Connected && HasStoredTokens(ownerAuth))
                continue;

            var orphan = await store.GetProfileByIdAsync(orphanSnapshot.Id, cancellationToken);
            if (orphan is null)
                continue;

            await MoveConversationsToProfileAsync(store, orphan.Id, canonicalProfile.Id, cancellationToken);
            store.RemoveSocialProfile(orphan);
        }
    }

    private async Task MoveConversationsToProfileAsync(
        IProcessDataStore store,
        Guid fromProfileId,
        Guid toProfileId,
        CancellationToken cancellationToken)
    {
        var conversations = await store.GetConversationsByProfileIdAsync(fromProfileId, cancellationToken);
        foreach (var conversation in conversations)
        {
            conversation.SocialProfileId = toProfileId;
            conversation.UpdatedAt = DateTime.UtcNow;
            store.UpdateConversation(conversation);
        }
    }

    private static bool HasStoredTokens(SocialAuthEntityBase? auth)
        => auth is not null
           && (!string.IsNullOrWhiteSpace(auth.AccessToken) || !string.IsNullOrWhiteSpace(auth.RefreshToken));

    private async Task QueueInitialSyncAsync(
        IProcessDataStore store,
        SocialAccountEntityBase account,
        CancellationToken cancellationToken)
    {
        var syncJob = store.NewSyncJob();
        syncJob.SocialAccountId = account.Id;
        syncJob.EntityType = SyncEntityType.Posts;
        syncJob.Status = SyncJobStatus.Pending;
        syncJob.StartedAt = DateTime.UtcNow;
        await store.AddSyncJobAsync(syncJob, cancellationToken);

        account.LastSyncAt = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;
        store.UpdateSocialAccount(account);
    }

    private static string? ResolveUserAccessToken(SocialAccountEntityBase? account)
        => ResolveUserAccessToken(account is null ? null : ProcessEntityNav.Auth(account));

    private static string? ResolveUserAccessToken(SocialAuthEntityBase? auth)
    {
        if (auth is null)
            return null;

        if (!string.IsNullOrWhiteSpace(auth.AccessToken))
            return auth.AccessToken;
        return !string.IsNullOrWhiteSpace(auth.RefreshToken) ? auth.RefreshToken : null;
    }

    /// <summary>
    /// Merges legacy duplicate <see cref="SocialAccount"/> rows (same user + platform) into one.
    /// Profiles/conversations move to the primary row; extra account rows are deleted.
    /// </summary>
    private async Task ConsolidateLegacyDuplicateAccountsAsync(
        IProcessDataStore store,
        Guid userId,
        Guid platformId,
        Guid primaryAccountId,
        CancellationToken cancellationToken)
    {
        var duplicateAccounts = (await store.GetSocialAccountsByUserAsync(userId, cancellationToken))
            .Where(a => a.PlatformId == platformId && a.Id != primaryAccountId)
            .ToList();
        if (duplicateAccounts.Count == 0)
            return;

        foreach (var duplicate in duplicateAccounts)
        {
            var profiles = await store.GetProfilesByAccountAsync(duplicate.Id, cancellationToken);
            foreach (var profile in profiles)
            {
                var tracked = await store.GetProfileByIdAsync(profile.Id, cancellationToken);
                if (tracked is null)
                    continue;

                tracked.SocialAccountId = primaryAccountId;
                tracked.UpdatedAt = DateTime.UtcNow;
                store.UpdateSocialProfile(tracked);
            }
        }

        await store.SaveChangesAsync(cancellationToken);

        foreach (var duplicate in duplicateAccounts)
        {
            var toRemove = await store.GetSocialAccountByIdAsync(duplicate.Id, cancellationToken);
            if (toRemove is null)
                continue;

            store.RemoveSocialAccount(toRemove);
            await store.SaveChangesAsync(cancellationToken);
        }
    }

    private static HashSet<string> ResolveConnectedPageIds(SocialAccountEntityBase? account, string platformCode)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (account is null)
            return ids;

        foreach (var profile in ProcessEntityNav.Profiles(account))
        {
            // Facebook profiles are the page itself; Instagram profiles keep the page id in metadata.
            if (platformCode == "facebook" && !string.IsNullOrWhiteSpace(profile.ExternalProfileId))
                ids.Add(profile.ExternalProfileId);

            var pageId = ReadJsonString(profile.MetadataJson, "pageId");
            if (!string.IsNullOrWhiteSpace(pageId))
                ids.Add(pageId!);
        }

        return ids;
    }

    private static string? ReadJsonString(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(propertyName, out var value) ? value.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static MetaPageDto MapPage(MetaPageInfo page, string platformCode, ISet<string> connectedPageIds)
    {
        var needsInstagram = platformCode == "instagram";
        var isEligible = !needsInstagram || !string.IsNullOrWhiteSpace(page.InstagramId);

        return new MetaPageDto
        {
            PageId = page.PageId,
            PageName = page.PageName,
            PageImage = needsInstagram ? page.InstagramImage ?? page.PageImage : page.PageImage,
            InstagramId = page.InstagramId,
            InstagramUsername = page.InstagramUsername,
            IsEligible = isEligible,
            IneligibleReason = isEligible ? null : "No Instagram Business account is linked to this page.",
            IsSelected = connectedPageIds.Contains(page.PageId)
        };
    }

    public async Task<ApiResponse<object>> DisconnectAsync(
        Guid userId,
        string platformCode,
        string menuType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMenu = MenuTypes.Normalize(menuType);
            var store = _processData.ForMenu(normalizedMenu);
            var platform = await store.GetPlatformByCodeAsync(platformCode, cancellationToken);
            if (platform is null)
                return ApiResponse<object>.Fail("Unknown platform.");

            var account = await store.GetSocialAccountByUserAndPlatformAsync(userId, platform.Id, cancellationToken);
            if (account is null)
                return ApiResponse<object>.Fail("Account not connected.");

            await ConsolidateLegacyDuplicateAccountsAsync(store, userId, platform.Id, account.Id, cancellationToken);
            account = await store.GetSocialAccountByUserAndPlatformAsync(userId, platform.Id, cancellationToken)
                ?? account;

            var code = platformCode.Trim().ToLowerInvariant();
            var auth = await store.GetSocialAuthByAccountIdAsync(account.Id, cancellationToken);

            if (SupportsPageSelection(code) && account.Status == SocialAccountStatus.Connected)
                await UnsubscribePageWebhooksAsync(code, account, auth?.AccessToken, cancellationToken);

            account.Status = SocialAccountStatus.Disconnected;
            account.UpdatedAt = DateTime.UtcNow;
            store.UpdateSocialAccount(account);

            if (auth is not null)
            {
                auth.AccessToken = string.Empty;
                auth.RefreshToken = null;
                auth.UpdatedAt = DateTime.UtcNow;
                store.UpdateSocialAuth(auth);
            }

            await store.SaveChangesAsync(cancellationToken);
            return ApiResponse<object>.Ok(new { }, "Account disconnected.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<IReadOnlyList<SocialAccountDto>>> GetConnectedAccountsAsync(
        Guid userId,
        string menuType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMenu = MenuTypes.Normalize(menuType);
            var store = _processData.ForMenu(normalizedMenu);
            var accounts = await store.GetSocialAccountsByUserAsync(userId, cancellationToken);
            var data = accounts
                .Where(a => a.Status == SocialAccountStatus.Connected)
                .Select(a => MapAccount(a, ProcessEntityNav.Platform(a)!, normalizedMenu))
                .ToList();
            return ApiResponse<IReadOnlyList<SocialAccountDto>>.Ok(data);
        }
        catch (Exception ex)
        {
            return ApiResponse<IReadOnlyList<SocialAccountDto>>.Fail(ex.Message);
        }
    }

    private static ProfileType ParseProfileType(string value) => value.ToLowerInvariant() switch
    {
        "facebookpage" or "page" => ProfileType.FacebookPage,
        "instagrambusiness" or "instagram" => ProfileType.InstagramBusiness,
        "instagramlogin" => ProfileType.InstagramLogin,
        "whatsappphone" or "whatsapp" => ProfileType.WhatsAppPhone,
        _ => ProfileType.Other
    };

    private static SocialAccountDto MapAccount(
        SocialAccountEntityBase account,
        PlatformEntityBase platform,
        string menuType) => new()
    {
        Id = account.Id,
        PlatformId = account.PlatformId,
        PlatformCode = platform.Code,
        MenuType = menuType,
        PlatformName = platform.Name,
        ExternalAccountId = account.ExternalAccountId,
        DisplayName = account.DisplayName,
        Username = account.Username,
        Status = account.Status,
        ConnectedAt = account.ConnectedAt,
        LastSyncAt = account.LastSyncAt,
        Profiles = ProcessEntityNav.Profiles(account).Select(p => new SocialProfileDto
        {
            Id = p.Id,
            ExternalProfileId = p.ExternalProfileId,
            ProfileType = p.ProfileType.ToString(),
            Name = p.Name,
            Username = p.Username
        }).ToList()
    };
}
