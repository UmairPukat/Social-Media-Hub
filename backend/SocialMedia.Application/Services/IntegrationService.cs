using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SocialMedia.Application.Auth;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Integration;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Settings;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Interfaces;

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
    private readonly IFacebookService _facebookService;
    private readonly IInstagramService _instagramService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly MetaSettings _meta;
    private readonly JwtSettings _jwt;
    private readonly IConfiguration _configuration;

    public IntegrationService(
        IUnitOfWork unitOfWork,
        IFacebookService facebookService,
        IInstagramService instagramService,
        IWhatsAppService whatsAppService,
        IOptions<MetaSettings> metaOptions,
        IOptions<JwtSettings> jwtOptions,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _facebookService = facebookService;
        _instagramService = instagramService;
        _whatsAppService = whatsAppService;
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
            var platforms = (await _unitOfWork.Platforms.GetActiveAsync(cancellationToken))
                .Where(p => string.Equals(p.MenuType, normalizedMenu, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var accounts = await _unitOfWork.SocialAccounts.GetByUserAsync(userId, cancellationToken);
            var byPlatform = accounts
                .Where(a => a.Status == SocialAccountStatus.Connected
                            && string.Equals(a.MenuType, normalizedMenu, StringComparison.OrdinalIgnoreCase))
                .GroupBy(a => a.PlatformId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(AccountHasToken)
                        .ThenByDescending(a => a.ConnectedAt ?? a.UpdatedAt ?? a.CreatedAt)
                        .First());

            var cards = platforms
                .Select(p =>
                {
                    var def = PlatformCatalog.Find(p.Code);
                    byPlatform.TryGetValue(p.Id, out var account);
                    return new PlatformCardDto
                    {
                        PlatformId = p.Id,
                        Code = p.Code,
                        MenuType = p.MenuType,
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
                        SupportsPosts = def?.SupportsPosts ?? false
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

    public Task<ApiResponse<BeginOAuthResponse>> BeginOAuthAsync(
        Guid userId,
        BeginOAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        var platformCode = (request.PlatformCode ?? string.Empty).Trim().ToLowerInvariant();
        var menuType = MenuTypes.Normalize(request.MenuType);
        if (platformCode is not ("facebook" or "instagram" or "instagram_login" or "whatsapp"))
            return Task.FromResult(ApiResponse<BeginOAuthResponse>.Fail($"Unsupported platform '{request.PlatformCode}'."));

        var appId = ResolveAppId(platformCode);
        if (string.IsNullOrWhiteSpace(appId) || appId.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(ApiResponse<BeginOAuthResponse>.Fail($"Meta App Id is not configured for {platformCode}."));

        var redirectUri = ResolveRedirectUri(platformCode, null);
        if (string.IsNullOrWhiteSpace(redirectUri))
            return Task.FromResult(ApiResponse<BeginOAuthResponse>.Fail("Redirect URI is not configured. Set metaRedirectUri to the backend Callback URL."));

        var version = ResolveGraphVersion(platformCode);
        var scopes = PlatformScopes[platformCode];
        var state = MetaOAuthState.Create(userId, platformCode, menuType, _jwt.SecretKey);

        var authUrl = platformCode == "instagram_login"
            ? "https://www.instagram.com/oauth/authorize"
              + $"?client_id={Uri.EscapeDataString(appId)}"
              + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
              + $"&state={Uri.EscapeDataString(state)}"
              + $"&scope={Uri.EscapeDataString(scopes)}"
              + "&response_type=code"
            : $"https://www.facebook.com/{version}/dialog/oauth"
              + $"?client_id={Uri.EscapeDataString(appId)}"
              + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
              + $"&state={Uri.EscapeDataString(state)}"
              + $"&scope={Uri.EscapeDataString(scopes)}"
              + "&response_type=code";

        return Task.FromResult(ApiResponse<BeginOAuthResponse>.Ok(new BeginOAuthResponse
        {
            AuthUrl = authUrl,
            RedirectUri = redirectUri,
            PlatformCode = platformCode,
            MenuType = menuType
        }));
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

            var redirectUri = ResolveRedirectUri(platformCode, request.RedirectUri);
            if (string.IsNullOrWhiteSpace(redirectUri))
                return ApiResponse<SocialAccountDto>.Fail("Redirect URI is not configured.");

            OAuthTokenResult token;
            (string Id, string Name) me;

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

            return await PersistConnectedAccountAsync(
                userId,
                platformCode,
                MenuTypes.Normalize(request.MenuType),
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
        var platform = await _unitOfWork.Platforms.GetByCodeAsync(platformCode, cancellationToken);
        if (platform is null)
            return ApiResponse<SocialAccountDto>.Fail($"Unknown platform '{platformCode}'.");

        var account = await _unitOfWork.SocialAccounts.GetByUserAndPlatformAsync(userId, platform.Id, normalizedMenu, cancellationToken);
        var isNewAccount = account is null;
        if (account is null)
        {
            account = new SocialAccount
            {
                UserId = userId,
                PlatformId = platform.Id,
                MenuType = normalizedMenu
            };
            await _unitOfWork.SocialAccounts.AddAsync(account, cancellationToken);
        }

        account.MenuType = normalizedMenu;
        account.ExternalAccountId = externalAccountId;
        account.DisplayName = displayName;
        account.Status = SocialAccountStatus.Connected;
        account.ConnectedAt = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;
        MarkUpdated(_unitOfWork.SocialAccounts, account, isNewAccount);

        // Insert the account before its auth row so the foreign key is already valid.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var auth = await _unitOfWork.SocialAuths.GetBySocialAccountIdAsync(account.Id, cancellationToken);
        var isNewAuth = auth is null;
        if (auth is null)
        {
            auth = new SocialAuth { SocialAccountId = account.Id };
            await _unitOfWork.SocialAuths.AddAsync(auth, cancellationToken);
        }

        auth.AccessToken = accessToken;
        // Keep the long-lived user token: AccessToken is later swapped for the selected
        // page token, but listing pages always needs the user token.
        auth.RefreshToken = accessToken;
        auth.ExpiresAt = expiresAt;
        auth.UpdatedAt = DateTime.UtcNow;
        MarkUpdated(_unitOfWork.SocialAuths, auth, isNewAuth);

        // Legacy App Connections may have left extra rows for the same user+platform.
        // Merge profiles into this account and delete duplicates — updating multiple rows
        // that share the unique (UserId, PlatformId, MenuType) index causes an EF circular dependency.
        await ConsolidateLegacyDuplicateAccountsAsync(userId, platform.Id, normalizedMenu, account.Id, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var persistedAuth = await _unitOfWork.SocialAuths.GetBySocialAccountIdAsync(account.Id, cancellationToken);
        if (persistedAuth is null || string.IsNullOrWhiteSpace(persistedAuth.AccessToken))
            return ApiResponse<SocialAccountDto>.Fail("The access token could not be saved. Disconnect and connect again.");

        var requiresPageSelection = SupportsPageSelection(platformCode);
        if (!requiresPageSelection)
        {
            IReadOnlyList<SocialProfileDraft> drafts = platformCode switch
            {
                "instagram_login" => await _instagramService.DiscoverInstagramLoginProfilesAsync(accessToken, cancellationToken),
                "whatsapp" => await _whatsAppService.DiscoverProfilesAsync(
                    accessToken, _meta.WhatsApp.PhoneNumberId, _meta.WhatsApp.WabaId, cancellationToken),
                _ => Array.Empty<SocialProfileDraft>()
            };

            foreach (var draft in drafts)
                await UpsertProfileAsync(account, draft, cancellationToken);

            await QueueInitialSyncAsync(account, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var reloaded = await _unitOfWork.SocialAccounts.GetWithAuthAndProfilesAsync(account.Id, cancellationToken);
        var dto = MapAccount(reloaded ?? account, platform);
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
            if (!SupportsPageSelection(code))
                return ApiResponse<IReadOnlyList<MetaPageDto>>.Fail($"Page selection is not available for '{platformCode}'.");

            var platform = await _unitOfWork.Platforms.GetByCodeAsync(code, cancellationToken);
            if (platform is null)
                return ApiResponse<IReadOnlyList<MetaPageDto>>.Fail("Unknown platform.");

            var account = await _unitOfWork.SocialAccounts.GetByUserAndPlatformAsync(userId, platform.Id, normalizedMenu, cancellationToken);
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
            if (!SupportsPageSelection(code))
                return ApiResponse<SocialAccountDto>.Fail($"Page selection is not available for '{request.PlatformCode}'.");
            if (string.IsNullOrWhiteSpace(request.PageId))
                return ApiResponse<SocialAccountDto>.Fail("Select a page first.");

            var platform = await _unitOfWork.Platforms.GetByCodeAsync(code, cancellationToken);
            if (platform is null)
                return ApiResponse<SocialAccountDto>.Fail("Unknown platform.");

            var account = await _unitOfWork.SocialAccounts.GetByUserAndPlatformAsync(userId, platform.Id, normalizedMenu, cancellationToken);
            var auth = account?.Auth;
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

            await UpsertProfileAsync(account, draft, cancellationToken);

            if (!string.IsNullOrWhiteSpace(page.PageAccessToken))
            {
                // Page token drives post / comment / message calls; the user token stays in RefreshToken.
                auth.AccessToken = page.PageAccessToken!;
                auth.UpdatedAt = DateTime.UtcNow;
                MarkUpdated(_unitOfWork.SocialAuths, auth, isNew: false);
            }

            account.Status = SocialAccountStatus.Connected;
            account.ConnectedAt ??= DateTime.UtcNow;
            account.MetadataJson = JsonSerializer.Serialize(new
            {
                selectedPageId = page.PageId,
                selectedPageName = page.PageName
            });
            await QueueInitialSyncAsync(account, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var subscribeWarning = await SubscribePageWebhooksAsync(code, page, cancellationToken);

            var reloaded = await _unitOfWork.SocialAccounts.GetWithAuthAndProfilesAsync(account.Id, cancellationToken);
            var message = string.IsNullOrWhiteSpace(subscribeWarning)
                ? $"{page.PageName} connected."
                : $"{page.PageName} connected, but webhook subscription failed: {subscribeWarning}";

            return ApiResponse<SocialAccountDto>.Ok(MapAccount(reloaded ?? account, platform), message);
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
            var platform = await _unitOfWork.Platforms.GetByCodeAsync(code, cancellationToken);
            if (platform is null)
                return ApiResponse<ConnectionDetailsDto>.Fail("Unknown platform.");

            var account = await _unitOfWork.SocialAccounts.GetByUserAndPlatformAsync(userId, platform.Id, normalizedMenu, cancellationToken);
            if (account is null || account.Status != SocialAccountStatus.Connected)
                return ApiResponse<ConnectionDetailsDto>.Fail($"{platform.Name} is not connected.");

            var auth = account.Auth
                ?? await _unitOfWork.SocialAuths.GetBySocialAccountIdAsync(account.Id, cancellationToken);
            var effectiveToken = ResolveUserAccessToken(auth is null ? account : new SocialAccount { Auth = auth });

            var profile = account.Profiles.FirstOrDefault();
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
                Profiles = account.Profiles.Select(p => new SocialProfileDto
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
        SocialAccount account,
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

    private static string? ResolveSelectedPageId(SocialAccount account, string platformCode)
    {
        var selected = ReadJsonString(account.MetadataJson, "selectedPageId");
        if (!string.IsNullOrWhiteSpace(selected))
            return selected;

        foreach (var profile in account.Profiles)
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

    private static bool AccountHasToken(SocialAccount account)
        => account.Auth is not null
           && (!string.IsNullOrWhiteSpace(account.Auth.AccessToken)
               || !string.IsNullOrWhiteSpace(account.Auth.RefreshToken));

    private static bool SupportsPageSelection(string platformCode) =>
        platformCode.Equals("facebook", StringComparison.OrdinalIgnoreCase) ||
        platformCode.Equals("instagram", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// EF already tracks a freshly added entity as Added; calling Update() on it flips the state
    /// to Modified and issues an UPDATE for a row that does not exist, which fails with
    /// "expected to affect 1 row(s), but actually affected 0 row(s)".
    /// </summary>
    private static void MarkUpdated<T>(IRepository<T> repository, T entity, bool isNew) where T : class
    {
        if (!isNew)
            repository.Update(entity);
    }

    private async Task<SocialProfile> UpsertProfileAsync(
        SocialAccount account,
        SocialProfileDraft draft,
        CancellationToken cancellationToken)
    {
        var profiles = await _unitOfWork.SocialProfiles.GetBySocialAccountAsync(account.Id, cancellationToken);
        var existingId = profiles.FirstOrDefault(p => p.ExternalProfileId == draft.ExternalProfileId)?.Id;

        var profile = existingId.HasValue
            ? await _unitOfWork.SocialProfiles.GetByIdAsync(existingId.Value, cancellationToken)
            : null;

        var isNew = profile is null;
        if (profile is null)
        {
            profile = new SocialProfile { SocialAccountId = account.Id };
            await _unitOfWork.SocialProfiles.AddAsync(profile, cancellationToken);
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
        MarkUpdated(_unitOfWork.SocialProfiles, profile, isNew);

        await ReassignOrphanProfilesAsync(account, profile.ExternalProfileId, cancellationToken);

        return profile;
    }

    /// <summary>
    /// Legacy App Connections could leave profiles on a tokenless duplicate account.
    /// Re-link matching profiles so inbox replies use the freshly connected account.
    /// </summary>
    private async Task ReassignOrphanProfilesAsync(
        SocialAccount account,
        string externalProfileId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalProfileId))
            return;

        var orphans = await _unitOfWork.SocialProfiles.FindAsync(
            p => p.ExternalProfileId == externalProfileId && p.SocialAccountId != account.Id,
            cancellationToken);
        foreach (var orphan in orphans)
        {
            var owner = await _unitOfWork.SocialAccounts.GetByIdAsync(orphan.SocialAccountId, cancellationToken);
            if (owner is null || owner.UserId != account.UserId || owner.PlatformId != account.PlatformId
                || !string.Equals(owner.MenuType, account.MenuType, StringComparison.OrdinalIgnoreCase))
                continue;

            orphan.SocialAccountId = account.Id;
            orphan.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.SocialProfiles.Update(orphan);
        }
    }

    private async Task QueueInitialSyncAsync(SocialAccount account, CancellationToken cancellationToken)
    {
        await _unitOfWork.SyncJobs.AddAsync(new SyncJob
        {
            SocialAccountId = account.Id,
            EntityType = SyncEntityType.Posts,
            Status = SyncJobStatus.Pending,
            StartedAt = DateTime.UtcNow
        }, cancellationToken);

        account.LastSyncAt = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;
    }

    private static string? ResolveUserAccessToken(SocialAccount? account)
        => ResolveUserAccessToken(account?.Auth);

    private static string? ResolveUserAccessToken(SocialAuth? auth)
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
        Guid userId,
        Guid platformId,
        string menuType,
        Guid primaryAccountId,
        CancellationToken cancellationToken)
    {
        var duplicateAccounts = await _unitOfWork.SocialAccounts.FindAsync(
            a => a.UserId == userId
                 && a.PlatformId == platformId
                 && a.MenuType == menuType
                 && a.Id != primaryAccountId,
            cancellationToken);
        if (duplicateAccounts.Count == 0)
            return;

        foreach (var duplicate in duplicateAccounts)
        {
            var profiles = await _unitOfWork.SocialProfiles.GetBySocialAccountAsync(duplicate.Id, cancellationToken);
            foreach (var profile in profiles)
            {
                var tracked = await _unitOfWork.SocialProfiles.GetByIdAsync(profile.Id, cancellationToken);
                if (tracked is null)
                    continue;

                tracked.SocialAccountId = primaryAccountId;
                tracked.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.SocialProfiles.Update(tracked);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var duplicate in duplicateAccounts)
        {
            var toRemove = await _unitOfWork.SocialAccounts.GetByIdAsync(duplicate.Id, cancellationToken);
            if (toRemove is null)
                continue;

            _unitOfWork.SocialAccounts.Remove(toRemove);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private static HashSet<string> ResolveConnectedPageIds(SocialAccount? account, string platformCode)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (account is null)
            return ids;

        foreach (var profile in account.Profiles)
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
            var platform = await _unitOfWork.Platforms.GetByCodeAsync(platformCode, cancellationToken);
            if (platform is null)
                return ApiResponse<object>.Fail("Unknown platform.");

            var normalizedMenu = MenuTypes.Normalize(menuType);
            var account = await _unitOfWork.SocialAccounts.GetByUserAndPlatformAsync(userId, platform.Id, normalizedMenu, cancellationToken);
            if (account is null)
                return ApiResponse<object>.Fail("Account not connected.");

            await ConsolidateLegacyDuplicateAccountsAsync(userId, platform.Id, normalizedMenu, account.Id, cancellationToken);
            account = await _unitOfWork.SocialAccounts.GetByUserAndPlatformAsync(userId, platform.Id, normalizedMenu, cancellationToken)
                ?? account;

            var code = platformCode.Trim().ToLowerInvariant();
            var auth = await _unitOfWork.SocialAuths.GetBySocialAccountIdAsync(account.Id, cancellationToken);

            if (SupportsPageSelection(code) && account.Status == SocialAccountStatus.Connected)
                await UnsubscribePageWebhooksAsync(code, account, auth?.AccessToken, cancellationToken);

            account.Status = SocialAccountStatus.Disconnected;
            account.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.SocialAccounts.Update(account);

            if (auth is not null)
            {
                auth.AccessToken = string.Empty;
                auth.RefreshToken = null;
                auth.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.SocialAuths.Update(auth);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
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
            var accounts = await _unitOfWork.SocialAccounts.GetByUserAsync(userId, cancellationToken);
            var data = accounts
                .Where(a => a.Status == SocialAccountStatus.Connected
                            && string.Equals(a.MenuType, normalizedMenu, StringComparison.OrdinalIgnoreCase))
                .Select(a => MapAccount(a, a.Platform!))
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

    private static SocialAccountDto MapAccount(SocialAccount account, Platform platform) => new()
    {
        Id = account.Id,
        PlatformId = account.PlatformId,
        PlatformCode = platform.Code,
        MenuType = account.MenuType,
        PlatformName = platform.Name,
        ExternalAccountId = account.ExternalAccountId,
        DisplayName = account.DisplayName,
        Username = account.Username,
        Status = account.Status,
        ConnectedAt = account.ConnectedAt,
        LastSyncAt = account.LastSyncAt,
        Profiles = account.Profiles.Select(p => new SocialProfileDto
        {
            Id = p.Id,
            ExternalProfileId = p.ExternalProfileId,
            ProfileType = p.ProfileType.ToString(),
            Name = p.Name,
            Username = p.Username
        }).ToList()
    };
}
