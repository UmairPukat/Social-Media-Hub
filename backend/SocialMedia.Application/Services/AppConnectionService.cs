using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SocialMedia.Application.Auth;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.AppConnections;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Integration;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Settings;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Interfaces;

namespace SocialMedia.Application.Services;

/// <summary>
/// Manages user-owned Meta app credentials and OAuth for App Connections.
/// Each connection uses its own App Id, secret, and callback URL.
/// </summary>
public class AppConnectionService : IAppConnectionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFacebookService _facebookService;
    private readonly IInstagramService _instagramService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IMetaOAuthClient _metaOAuth;
    private readonly MetaSettings _meta;
    private readonly JwtSettings _jwt;
    private readonly IConfiguration _configuration;

    public AppConnectionService(
        IUnitOfWork unitOfWork,
        IFacebookService facebookService,
        IInstagramService instagramService,
        IWhatsAppService whatsAppService,
        IMetaOAuthClient metaOAuth,
        IOptions<MetaSettings> metaOptions,
        IOptions<JwtSettings> jwtOptions,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _facebookService = facebookService;
        _instagramService = instagramService;
        _whatsAppService = whatsAppService;
        _metaOAuth = metaOAuth;
        _meta = metaOptions.Value;
        _jwt = jwtOptions.Value;
        _configuration = configuration;
    }

    public async Task<ApiResponse<IReadOnlyList<MetaAppConnectionDto>>> GetAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connections = await _unitOfWork.MetaAppConnections.GetByUserAsync(userId, cancellationToken);
            var accounts = await _unitOfWork.SocialAccounts.GetByUserAsync(userId, cancellationToken);
            var connectedByApp = accounts
                .Where(a => a.MetaAppConnectionId.HasValue && a.Status == SocialAccountStatus.Connected)
                .ToDictionary(a => a.MetaAppConnectionId!.Value);

            var dtos = connections.Select(c =>
            {
                connectedByApp.TryGetValue(c.Id, out var account);
                var def = PlatformCatalog.Find(c.PlatformCode);
                return MapConnection(c, def, account);
            }).ToList();

            return ApiResponse<IReadOnlyList<MetaAppConnectionDto>>.Ok(dtos);
        }
        catch (Exception ex)
        {
            return ApiResponse<IReadOnlyList<MetaAppConnectionDto>>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<MetaAppConnectionDto>> CreateAsync(
        Guid userId,
        CreateMetaAppConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var platformCode = NormalizePlatform(request.PlatformCode);
            if (platformCode is null)
                return ApiResponse<MetaAppConnectionDto>.Fail("Platform must be facebook, instagram, instagram_login, or whatsapp.");

            if (string.IsNullOrWhiteSpace(request.Name))
                return ApiResponse<MetaAppConnectionDto>.Fail("Name is required.");
            if (string.IsNullOrWhiteSpace(request.AppId))
                return ApiResponse<MetaAppConnectionDto>.Fail("App Id is required.");
            if (string.IsNullOrWhiteSpace(request.AppSecret))
                return ApiResponse<MetaAppConnectionDto>.Fail("App Secret is required.");
            if (string.IsNullOrWhiteSpace(request.CallbackUrl))
                return ApiResponse<MetaAppConnectionDto>.Fail("Callback URL is required.");

            var entity = new MetaAppConnection
            {
                UserId = userId,
                Name = request.Name.Trim(),
                PlatformCode = platformCode,
                AppId = request.AppId.Trim(),
                AppSecret = request.AppSecret.Trim(),
                CallbackUrl = request.CallbackUrl.Trim(),
                GraphApiVersion = string.IsNullOrWhiteSpace(request.GraphApiVersion) ? "v21.0" : request.GraphApiVersion.Trim(),
                Scopes = ResolveScopes(platformCode, request.Scopes)
            };

            await _unitOfWork.MetaAppConnections.AddAsync(entity, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var def = PlatformCatalog.Find(platformCode);
            return ApiResponse<MetaAppConnectionDto>.Ok(MapConnection(entity, def, null), "App connection created.");
        }
        catch (Exception ex)
        {
            return ApiResponse<MetaAppConnectionDto>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<MetaAppConnectionDto>> UpdateAsync(
        Guid userId,
        Guid id,
        UpdateMetaAppConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _unitOfWork.MetaAppConnections.GetByIdForUserAsync(id, userId, cancellationToken);
            if (entity is null)
                return ApiResponse<MetaAppConnectionDto>.Fail("App connection not found.");

            if (string.IsNullOrWhiteSpace(request.Name))
                return ApiResponse<MetaAppConnectionDto>.Fail("Name is required.");
            if (string.IsNullOrWhiteSpace(request.AppId))
                return ApiResponse<MetaAppConnectionDto>.Fail("App Id is required.");
            if (string.IsNullOrWhiteSpace(request.AppSecret))
                return ApiResponse<MetaAppConnectionDto>.Fail("App Secret is required.");
            if (string.IsNullOrWhiteSpace(request.CallbackUrl))
                return ApiResponse<MetaAppConnectionDto>.Fail("Callback URL is required.");

            entity.Name = request.Name.Trim();
            entity.AppId = request.AppId.Trim();
            entity.AppSecret = request.AppSecret.Trim();
            entity.CallbackUrl = request.CallbackUrl.Trim();
            entity.GraphApiVersion = string.IsNullOrWhiteSpace(request.GraphApiVersion) ? entity.GraphApiVersion : request.GraphApiVersion.Trim();
            entity.Scopes = ResolveScopes(entity.PlatformCode, request.Scopes);
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.MetaAppConnections.Update(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var account = await FindLinkedAccountAsync(userId, entity, cancellationToken);
            var def = PlatformCatalog.Find(entity.PlatformCode);
            return ApiResponse<MetaAppConnectionDto>.Ok(MapConnection(entity, def, account), "App connection updated.");
        }
        catch (Exception ex)
        {
            return ApiResponse<MetaAppConnectionDto>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _unitOfWork.MetaAppConnections.GetByIdForUserAsync(id, userId, cancellationToken);
            if (entity is null)
                return ApiResponse<object>.Fail("App connection not found.");

            var account = await FindLinkedAccountAsync(userId, entity, cancellationToken);
            if (account is not null && account.Status == SocialAccountStatus.Connected)
                await DisconnectAccountAsync(entity.PlatformCode, account, cancellationToken);

            _unitOfWork.MetaAppConnections.Remove(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<object>.Ok(new { }, "App connection deleted.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<BeginAppConnectionOAuthResponse>> BeginOAuthAsync(
        Guid userId,
        BeginAppConnectionOAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.MetaAppConnections.GetByIdForUserAsync(request.AppConnectionId, userId, cancellationToken);
        if (entity is null)
            return ApiResponse<BeginAppConnectionOAuthResponse>.Fail("App connection not found.");

        var platformCode = entity.PlatformCode;
        if (string.IsNullOrWhiteSpace(entity.AppId) || string.IsNullOrWhiteSpace(entity.AppSecret))
            return ApiResponse<BeginAppConnectionOAuthResponse>.Fail("App Id and App Secret must be configured.");
        if (string.IsNullOrWhiteSpace(entity.CallbackUrl))
            return ApiResponse<BeginAppConnectionOAuthResponse>.Fail("Callback URL must be configured.");

        var scopes = ResolveScopes(entity.PlatformCode, entity.Scopes);
        if (string.IsNullOrWhiteSpace(scopes))
            return ApiResponse<BeginAppConnectionOAuthResponse>.Fail("OAuth scopes must be configured.");

        var state = MetaOAuthState.Create(userId, platformCode, _jwt.SecretKey, entity.Id);
        var version = entity.GraphApiVersion;

        var authUrl = platformCode == "instagram_login"
            ? "https://www.instagram.com/oauth/authorize"
              + $"?client_id={Uri.EscapeDataString(entity.AppId)}"
              + $"&redirect_uri={Uri.EscapeDataString(entity.CallbackUrl)}"
              + $"&state={Uri.EscapeDataString(state)}"
              + $"&scope={Uri.EscapeDataString(scopes)}"
              + "&response_type=code"
            : $"https://www.facebook.com/{version}/dialog/oauth"
              + $"?client_id={Uri.EscapeDataString(entity.AppId)}"
              + $"&redirect_uri={Uri.EscapeDataString(entity.CallbackUrl)}"
              + $"&state={Uri.EscapeDataString(state)}"
              + $"&scope={Uri.EscapeDataString(scopes)}"
              + "&response_type=code";

        return ApiResponse<BeginAppConnectionOAuthResponse>.Ok(new BeginAppConnectionOAuthResponse
        {
            AuthUrl = authUrl,
            RedirectUri = entity.CallbackUrl,
            PlatformCode = platformCode,
            AppConnectionId = entity.Id
        });
    }

    public async Task<AppConnectionMetaRedirectResult> CompleteMetaRedirectAsync(
        string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken = default)
    {
        var origins = ResolveFrontendOrigins();

        if (!string.IsNullOrWhiteSpace(error))
        {
            return new AppConnectionMetaRedirectResult
            {
                Ok = false,
                Message = error!,
                FrontendOrigins = origins
            };
        }

        if (!MetaOAuthState.TryValidate(state, _jwt.SecretKey, out var userId, out var platformCode, out var appConnectionId, out var stateError))
        {
            return new AppConnectionMetaRedirectResult
            {
                Ok = false,
                Message = stateError,
                FrontendOrigins = origins
            };
        }

        if (!appConnectionId.HasValue)
        {
            return new AppConnectionMetaRedirectResult
            {
                Ok = false,
                Message = "Missing app connection in OAuth state.",
                FrontendOrigins = origins
            };
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return new AppConnectionMetaRedirectResult
            {
                Ok = false,
                PlatformCode = platformCode,
                AppConnectionId = appConnectionId.Value,
                Message = "Missing authorization code.",
                FrontendOrigins = origins
            };
        }

        var response = await ExchangeAuthCodeAsync(userId, appConnectionId.Value, code, cancellationToken);

        return new AppConnectionMetaRedirectResult
        {
            Ok = response.Success,
            PlatformCode = platformCode,
            AppConnectionId = appConnectionId.Value,
            Message = response.Success
                ? (response.Message ?? "Connected. You can close this window.")
                : (response.Message ?? "Connection failed."),
            FrontendOrigins = origins
        };
    }

    public async Task<ApiResponse<IReadOnlyList<MetaPageDto>>> GetPagesAsync(
        Guid userId,
        Guid appConnectionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (appConnectionId == Guid.Empty)
                return ApiResponse<IReadOnlyList<MetaPageDto>>.Fail("App connection id is required.");

            var entity = await _unitOfWork.MetaAppConnections.GetByIdForUserAsync(appConnectionId, userId, cancellationToken);
            if (entity is null)
                return ApiResponse<IReadOnlyList<MetaPageDto>>.Fail("App connection not found.");

            if (!SupportsPageSelection(entity.PlatformCode))
                return ApiResponse<IReadOnlyList<MetaPageDto>>.Fail("Page selection is not available for this platform.");

            var resolved = await ResolveAccountForPageFlowAsync(userId, entity, cancellationToken);
            if (resolved is null)
                return ApiResponse<IReadOnlyList<MetaPageDto>>.Fail("Sign in with Meta again — no stored login token was found.");

            var (account, _, userToken) = resolved.Value;

            var pages = await ListPagesAsync(entity.PlatformCode, userToken, cancellationToken);
            var connectedPageIds = ResolveConnectedPageIds(account, entity.PlatformCode);
            var data = pages
                .Select(p => MapPage(p, entity.PlatformCode, connectedPageIds))
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
        AppConnectionSelectPageRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request.AppConnectionId == Guid.Empty)
                return ApiResponse<SocialAccountDto>.Fail("App connection id is required.");

            var entity = await _unitOfWork.MetaAppConnections.GetByIdForUserAsync(request.AppConnectionId, userId, cancellationToken);
            if (entity is null)
                return ApiResponse<SocialAccountDto>.Fail("App connection not found.");

            if (!SupportsPageSelection(entity.PlatformCode))
                return ApiResponse<SocialAccountDto>.Fail("Page selection is not available for this platform.");
            if (string.IsNullOrWhiteSpace(request.PageId))
                return ApiResponse<SocialAccountDto>.Fail("Select a page first.");

            var platform = await _unitOfWork.Platforms.GetByCodeAsync(entity.PlatformCode, cancellationToken);
            if (platform is null)
                return ApiResponse<SocialAccountDto>.Fail("Unknown platform.");

            var resolved = await ResolveAccountForPageFlowAsync(userId, entity, cancellationToken);
            if (resolved is null)
                return ApiResponse<SocialAccountDto>.Fail("Sign in with Meta before selecting a page.");

            var (account, auth, userToken) = resolved.Value;

            var pages = await ListPagesAsync(entity.PlatformCode, userToken, cancellationToken);
            var page = pages.FirstOrDefault(p => p.PageId == request.PageId);
            if (page is null)
                return ApiResponse<SocialAccountDto>.Fail("That page is no longer granted to this Meta login. Reconnect and try again.");

            if (entity.PlatformCode == "instagram" && string.IsNullOrWhiteSpace(page.InstagramId))
                return ApiResponse<SocialAccountDto>.Fail($"'{page.PageName}' has no Instagram Business account linked to it.");

            var linkedName = entity.PlatformCode == "instagram"
                ? page.InstagramName ?? page.InstagramUsername ?? page.PageName
                : page.PageName;

            var draft = entity.PlatformCode == "instagram"
                ? new SocialProfileDraft
                {
                    ExternalProfileId = page.InstagramId!,
                    Name = linkedName,
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
                // Page token drives API calls; the user token stays in RefreshToken for future page listing.
                auth.AccessToken = page.PageAccessToken!;
                auth.UpdatedAt = DateTime.UtcNow;
                MarkUpdated(_unitOfWork.SocialAuths, auth, isNew: false);
            }

            account.Status = SocialAccountStatus.Connected;
            account.ConnectedAt ??= DateTime.UtcNow;
            account.DisplayName = linkedName;
            account.UpdatedAt = DateTime.UtcNow;
            account.MetadataJson = JsonSerializer.Serialize(new
            {
                selectedPageId = page.PageId,
                selectedPageName = linkedName
            });
            MarkUpdated(_unitOfWork.SocialAccounts, account, isNew: false);
            await QueueInitialSyncAsync(account, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var subscribeWarning = await SubscribePageWebhooksAsync(entity.PlatformCode, page, cancellationToken);
            var reloaded = await _unitOfWork.SocialAccounts.GetWithAuthAndProfilesAsync(account.Id, cancellationToken);
            var message = string.IsNullOrWhiteSpace(subscribeWarning)
                ? $"{page.PageName} connected."
                : $"{page.PageName} connected, but webhook subscription failed: {subscribeWarning}";

            var dto = MapAccount(reloaded ?? account, platform);
            dto.RequiresPageSelection = false;
            return ApiResponse<SocialAccountDto>.Ok(dto, message);
        }
        catch (Exception ex)
        {
            return ApiResponse<SocialAccountDto>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<AppConnectionConnectionDetailsDto>> GetConnectionDetailsAsync(
        Guid userId,
        Guid appConnectionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _unitOfWork.MetaAppConnections.GetByIdForUserAsync(appConnectionId, userId, cancellationToken);
            if (entity is null)
                return ApiResponse<AppConnectionConnectionDetailsDto>.Fail("App connection not found.");

            var platform = await _unitOfWork.Platforms.GetByCodeAsync(entity.PlatformCode, cancellationToken);
            if (platform is null)
                return ApiResponse<AppConnectionConnectionDetailsDto>.Fail("Unknown platform.");

            var account = await FindLinkedAccountAsync(userId, entity, cancellationToken);
            if (account is null || account.Status != SocialAccountStatus.Connected)
                return ApiResponse<AppConnectionConnectionDetailsDto>.Fail($"{entity.Name} is not connected.");

            var profile = account.Profiles.FirstOrDefault();
            var pageId = ResolveSelectedPageId(account, entity.PlatformCode);
            var isInstagram = entity.PlatformCode is "instagram" or "instagram_login";

            var details = new AppConnectionConnectionDetailsDto
            {
                AppConnectionId = entity.Id,
                AppConnectionName = entity.Name,
                PlatformCode = platform.Code,
                PlatformName = platform.Name,
                AccountName = account.DisplayName,
                Status = account.Status,
                ConnectedAt = account.ConnectedAt,
                LastSyncAt = account.LastSyncAt,
                PageId = entity.PlatformCode == "instagram_login" ? null : pageId,
                PageName = entity.PlatformCode == "instagram_login"
                    ? (profile?.Name ?? account.DisplayName)
                    : (ReadJsonString(account.MetadataJson, "selectedPageName") ?? profile?.Name),
                PageImage = profile?.ProfileImage,
                InstagramId = isInstagram ? profile?.ExternalProfileId : null,
                InstagramUsername = isInstagram ? profile?.Username : null,
                AccessToken = string.IsNullOrWhiteSpace(account.Auth?.AccessToken) ? null : account.Auth!.AccessToken,
                AppId = entity.AppId,
                CallbackUrl = entity.CallbackUrl,
                Scopes = entity.Scopes,
                Profiles = account.Profiles.Select(p => new SocialProfileDto
                {
                    Id = p.Id,
                    ExternalProfileId = p.ExternalProfileId,
                    ProfileType = p.ProfileType.ToString(),
                    Name = p.Name,
                    Username = p.Username
                }).ToList()
            };

            await ApplyWebhookStatusAsync(details, entity.PlatformCode, pageId, account.Auth?.AccessToken, cancellationToken);
            return ApiResponse<AppConnectionConnectionDetailsDto>.Ok(details);
        }
        catch (Exception ex)
        {
            return ApiResponse<AppConnectionConnectionDetailsDto>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<object>> DisconnectAsync(
        Guid userId,
        Guid appConnectionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _unitOfWork.MetaAppConnections.GetByIdForUserAsync(appConnectionId, userId, cancellationToken);
            if (entity is null)
                return ApiResponse<object>.Fail("App connection not found.");

            var account = await FindLinkedAccountAsync(userId, entity, cancellationToken);
            if (account is null)
                return ApiResponse<object>.Fail("Account not connected.");

            await DisconnectAccountAsync(entity.PlatformCode, account, cancellationToken);
            return ApiResponse<object>.Ok(new { }, "Account disconnected.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    private async Task<ApiResponse<SocialAccountDto>> ExchangeAuthCodeAsync(
        Guid userId,
        Guid appConnectionId,
        string code,
        CancellationToken cancellationToken)
    {
        try
        {
            var entity = await _unitOfWork.MetaAppConnections.GetByIdForUserAsync(appConnectionId, userId, cancellationToken);
            if (entity is null)
                return ApiResponse<SocialAccountDto>.Fail("App connection not found.");

            var credentials = new MetaOAuthCredentials(
                entity.AppId,
                entity.AppSecret,
                entity.CallbackUrl,
                entity.GraphApiVersion);

            var token = await _metaOAuth.ExchangeCodeAsync(entity.PlatformCode, credentials, code, cancellationToken);
            var me = await _metaOAuth.GetMeAsync(entity.PlatformCode, entity.GraphApiVersion, token.AccessToken, cancellationToken);

            return await PersistConnectedAccountAsync(
                userId,
                entity,
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

    private async Task<ApiResponse<SocialAccountDto>> PersistConnectedAccountAsync(
        Guid userId,
        MetaAppConnection entity,
        string accessToken,
        DateTime? expiresAt,
        string externalAccountId,
        string displayName,
        CancellationToken cancellationToken)
    {
        var platform = await _unitOfWork.Platforms.GetByCodeAsync(entity.PlatformCode, cancellationToken);
        if (platform is null)
            return ApiResponse<SocialAccountDto>.Fail($"Unknown platform '{entity.PlatformCode}'.");

        var account = await _unitOfWork.SocialAccounts.GetByUserPlatformAndAppConnectionAsync(
            userId, platform.Id, entity.Id, cancellationToken);
        var isNewAccount = account is null;
        if (account is null)
        {
            account = new SocialAccount
            {
                UserId = userId,
                PlatformId = platform.Id,
                MetaAppConnectionId = entity.Id
            };
            await _unitOfWork.SocialAccounts.AddAsync(account, cancellationToken);
        }

        account.ExternalAccountId = externalAccountId;
        account.DisplayName = displayName;
        account.Status = SocialAccountStatus.Connected;
        account.ConnectedAt = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;
        MarkUpdated(_unitOfWork.SocialAccounts, account, isNewAccount);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var auth = await _unitOfWork.SocialAuths.GetBySocialAccountIdAsync(account.Id, cancellationToken);
        var isNewAuth = auth is null;
        if (auth is null)
        {
            auth = new SocialAuth { SocialAccountId = account.Id };
            await _unitOfWork.SocialAuths.AddAsync(auth, cancellationToken);
        }

        auth.AccessToken = accessToken;
        auth.RefreshToken = accessToken;
        auth.ExpiresAt = expiresAt;
        auth.UpdatedAt = DateTime.UtcNow;
        MarkUpdated(_unitOfWork.SocialAuths, auth, isNewAuth);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var requiresPageSelection = SupportsPageSelection(entity.PlatformCode);
        if (requiresPageSelection)
        {
            await ClearPageSelectionAsync(account, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else
        {
            IReadOnlyList<SocialProfileDraft> drafts = entity.PlatformCode switch
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

    private async Task<SocialAccount?> FindLinkedAccountAsync(
        Guid userId,
        MetaAppConnection entity,
        CancellationToken cancellationToken)
    {
        var platform = await _unitOfWork.Platforms.GetByCodeAsync(entity.PlatformCode, cancellationToken);
        if (platform is null)
            return null;

        return await _unitOfWork.SocialAccounts.GetByUserPlatformAndAppConnectionAsync(
            userId, platform.Id, entity.Id, cancellationToken);
    }

    private async Task DisconnectAccountAsync(string platformCode, SocialAccount account, CancellationToken cancellationToken)
    {
        var code = platformCode.Trim().ToLowerInvariant();
        var auth = await _unitOfWork.SocialAuths.GetBySocialAccountIdAsync(account.Id, cancellationToken);

        if (SupportsPageSelection(code))
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

        if (SupportsPageSelection(code))
            await ClearPageSelectionAsync(account, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static MetaAppConnectionDto MapConnection(
        MetaAppConnection entity,
        PlatformDefinition? def,
        SocialAccount? account) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        PlatformCode = entity.PlatformCode,
        PlatformName = def?.Name ?? entity.PlatformCode,
        AppId = entity.AppId,
        CallbackUrl = entity.CallbackUrl,
        GraphApiVersion = entity.GraphApiVersion,
        Scopes = entity.Scopes,
        IsConnected = account?.Status == SocialAccountStatus.Connected,
        AccountName = ResolveLinkedAccountName(account, entity.PlatformCode),
        ConnectedAt = account?.ConnectedAt,
        SupportsComments = def?.SupportsComments ?? false,
        SupportsMessages = def?.SupportsMessages ?? false,
        SupportsPosts = def?.SupportsPosts ?? false,
        CanConnect = def?.CanConnect ?? true,
        RequiresPageSelection = account is not null
            && account.Status == SocialAccountStatus.Connected
            && SupportsPageSelection(entity.PlatformCode)
            && string.IsNullOrWhiteSpace(ResolveSelectedPageId(account, entity.PlatformCode))
    };

    public Task<ApiResponse<AppConnectionDefaultScopesDto>> GetDefaultScopesAsync(
        string platformCode,
        CancellationToken cancellationToken = default)
    {
        var code = NormalizePlatform(platformCode);
        if (code is null)
            return Task.FromResult(ApiResponse<AppConnectionDefaultScopesDto>.Fail(
                "Platform must be facebook, instagram, instagram_login, or whatsapp."));

        return Task.FromResult(ApiResponse<AppConnectionDefaultScopesDto>.Ok(new AppConnectionDefaultScopesDto
        {
            PlatformCode = code,
            Scopes = AppConnectionScopeCatalog.GetDefault(code)
        }));
    }

    private static string ResolveScopes(string platformCode, string? scopes)
    {
        if (!string.IsNullOrWhiteSpace(scopes))
            return scopes.Trim();

        return AppConnectionScopeCatalog.GetDefault(platformCode);
    }

    private static string? NormalizePlatform(string? code)
    {
        var value = (code ?? string.Empty).Trim().ToLowerInvariant();
        return value is "facebook" or "instagram" or "instagram_login" or "whatsapp" ? value : null;
    }

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

    private async Task ApplyWebhookStatusAsync(
        AppConnectionConnectionDetailsDto details,
        string platformCode,
        string? pageId,
        string? pageAccessToken,
        CancellationToken cancellationToken)
    {
        if (!SupportsPageSelection(platformCode))
        {
            if (platformCode == "instagram_login")
                details.WebhookError = "Instagram Login webhooks are configured in the Meta App Dashboard.";
            return;
        }

        if (string.IsNullOrWhiteSpace(pageId) || string.IsNullOrWhiteSpace(pageAccessToken))
        {
            details.WebhookError = "No page is selected yet.";
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

    private async Task<string?> SubscribePageWebhooksAsync(string platformCode, MetaPageInfo page, CancellationToken cancellationToken)
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
            // Local disconnect still proceeds.
        }
    }

    private async Task<IReadOnlyList<MetaPageInfo>> ListPagesAsync(
        string platformCode,
        string userAccessToken,
        CancellationToken cancellationToken)
    {
        return platformCode == "instagram"
            ? await _instagramService.ListPagesAsync(userAccessToken, cancellationToken)
            : await _facebookService.ListPagesAsync(userAccessToken, cancellationToken);
    }

    private static bool SupportsPageSelection(string platformCode) =>
        platformCode.Equals("facebook", StringComparison.OrdinalIgnoreCase) ||
        platformCode.Equals("instagram", StringComparison.OrdinalIgnoreCase);

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
        return profile;
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

    private async Task<(SocialAccount Account, SocialAuth Auth, string UserToken)?> ResolveAccountForPageFlowAsync(
        Guid userId,
        MetaAppConnection entity,
        CancellationToken cancellationToken)
    {
        var account = await FindLinkedAccountAsync(userId, entity, cancellationToken);
        if (account is null)
            return null;

        var auth = account.Auth;
        if (auth is null)
            auth = await _unitOfWork.SocialAuths.GetBySocialAccountIdAsync(account.Id, cancellationToken);

        if (auth is null)
            return null;

        var userToken = ResolveUserAccessToken(account, auth);
        if (string.IsNullOrWhiteSpace(userToken))
            return null;

        return (account, auth, userToken);
    }

    private async Task ClearPageSelectionAsync(SocialAccount account, CancellationToken cancellationToken)
    {
        account.MetadataJson = null;
        account.UpdatedAt = DateTime.UtcNow;
        MarkUpdated(_unitOfWork.SocialAccounts, account, isNew: false);

        var profiles = await _unitOfWork.SocialProfiles.GetBySocialAccountAsync(account.Id, cancellationToken);
        foreach (var profile in profiles.Where(p =>
                     p.ProfileType is ProfileType.FacebookPage or ProfileType.InstagramBusiness))
        {
            var tracked = await _unitOfWork.SocialProfiles.GetByIdAsync(profile.Id, cancellationToken);
            if (tracked is not null)
                _unitOfWork.SocialProfiles.Remove(tracked);
        }
    }

    private static string? ResolveLinkedAccountName(SocialAccount? account, string platformCode)
    {
        if (account is null)
            return null;

        var selectedName = ReadJsonString(account.MetadataJson, "selectedPageName");
        if (!string.IsNullOrWhiteSpace(selectedName))
            return selectedName;

        if (!string.IsNullOrWhiteSpace(ResolveSelectedPageId(account, platformCode)))
            return account.Profiles.FirstOrDefault()?.Name ?? account.DisplayName;

        return account.DisplayName;
    }

    private static string? ResolveUserAccessToken(SocialAccount? account, SocialAuth? auth = null)
    {
        auth ??= account?.Auth;
        if (auth is null)
            return null;

        return !string.IsNullOrWhiteSpace(auth.RefreshToken) ? auth.RefreshToken : auth.AccessToken;
    }

    private static HashSet<string> ResolveConnectedPageIds(SocialAccount? account, string platformCode)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (account is null)
            return ids;

        foreach (var profile in account.Profiles)
        {
            if (platformCode == "facebook" && !string.IsNullOrWhiteSpace(profile.ExternalProfileId))
                ids.Add(profile.ExternalProfileId);

            var pageId = ReadJsonString(profile.MetadataJson, "pageId");
            if (!string.IsNullOrWhiteSpace(pageId))
                ids.Add(pageId!);
        }

        return ids;
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
