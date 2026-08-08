using System.Text.Json;
using Microsoft.Extensions.Options;
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
/// OAuth callbacks exchange Meta authorization codes, then store SocialAccount / SocialAuth / profiles.
/// </summary>
public class IntegrationService : IIntegrationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFacebookService _facebookService;
    private readonly IInstagramService _instagramService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly MetaSettings _meta;

    public IntegrationService(
        IUnitOfWork unitOfWork,
        IFacebookService facebookService,
        IInstagramService instagramService,
        IWhatsAppService whatsAppService,
        IOptions<MetaSettings> metaOptions)
    {
        _unitOfWork = unitOfWork;
        _facebookService = facebookService;
        _instagramService = instagramService;
        _whatsAppService = whatsAppService;
        _meta = metaOptions.Value;
    }

    public async Task<ApiResponse<IReadOnlyList<PlatformCardDto>>> GetPlatformCardsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var platforms = await _unitOfWork.Platforms.GetActiveAsync(cancellationToken);
            var accounts = await _unitOfWork.SocialAccounts.GetByUserAsync(userId, cancellationToken);
            var byPlatform = accounts.Where(a => a.Status == SocialAccountStatus.Connected)
                .ToDictionary(a => a.PlatformId);

            var cards = platforms
                .Select(p =>
                {
                    var def = PlatformCatalog.Find(p.Code);
                    byPlatform.TryGetValue(p.Id, out var account);
                    return new PlatformCardDto
                    {
                        PlatformId = p.Id,
                        Code = p.Code,
                        DisplayName = def?.Name ?? p.Name,
                        Icon = def?.Icon ?? p.Icon,
                        Description = def?.Description ?? $"{p.Name} integration",
                        Category = def?.Category ?? "other",
                        CategoryLabel = def?.CategoryLabel ?? "Other",
                        SortOrder = def?.SortOrder ?? 9999,
                        CanConnect = def?.CanConnect ?? false,
                        IsConnected = account is not null,
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

    public Task<ApiResponse<SocialAccountDto>> FacebookCallbackAsync(Guid userId, OAuthCallbackRequest request, CancellationToken cancellationToken = default)
        => HandleMetaCallbackAsync(userId, "facebook", request, cancellationToken);

    public Task<ApiResponse<SocialAccountDto>> InstagramCallbackAsync(Guid userId, OAuthCallbackRequest request, CancellationToken cancellationToken = default)
        => HandleMetaCallbackAsync(userId, "instagram", request, cancellationToken);

    public Task<ApiResponse<SocialAccountDto>> WhatsAppCallbackAsync(Guid userId, OAuthCallbackRequest request, CancellationToken cancellationToken = default)
        => HandleMetaCallbackAsync(userId, "whatsapp", request, cancellationToken);

    private async Task<ApiResponse<SocialAccountDto>> HandleMetaCallbackAsync(
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
            _meta.WhatsApp.RedirectUri);

        return platformCode switch
        {
            "facebook" => FirstNonEmpty(_meta.Facebook.RedirectUri, shared),
            "instagram" => FirstNonEmpty(_meta.Instagram.RedirectUri, _meta.Facebook.RedirectUri, shared),
            "whatsapp" => FirstNonEmpty(_meta.WhatsApp.RedirectUri, shared),
            _ => string.Empty
        };
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;

    private async Task<ApiResponse<SocialAccountDto>> PersistConnectedAccountAsync(
        Guid userId,
        string platformCode,
        string accessToken,
        DateTime? expiresAt,
        string externalAccountId,
        string displayName,
        CancellationToken cancellationToken)
    {
        var platform = await _unitOfWork.Platforms.GetByCodeAsync(platformCode, cancellationToken);
        if (platform is null)
            return ApiResponse<SocialAccountDto>.Fail($"Unknown platform '{platformCode}'.");

        var account = await _unitOfWork.SocialAccounts.GetByUserAndPlatformAsync(userId, platform.Id, cancellationToken);
        var isNewAccount = account is null;
        if (account is null)
        {
            account = new SocialAccount
            {
                UserId = userId,
                PlatformId = platform.Id
            };
            await _unitOfWork.SocialAccounts.AddAsync(account, cancellationToken);
        }

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

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var requiresPageSelection = SupportsPageSelection(platformCode);
        if (!requiresPageSelection)
        {
            // WhatsApp resolves to a single phone number, so there is nothing to pick.
            var drafts = await _whatsAppService.DiscoverProfilesAsync(
                accessToken, _meta.WhatsApp.PhoneNumberId, _meta.WhatsApp.WabaId, cancellationToken);

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
        CancellationToken cancellationToken = default)
    {
        try
        {
            var code = (platformCode ?? string.Empty).Trim().ToLowerInvariant();
            if (!SupportsPageSelection(code))
                return ApiResponse<IReadOnlyList<MetaPageDto>>.Fail($"Page selection is not available for '{platformCode}'.");

            var platform = await _unitOfWork.Platforms.GetByCodeAsync(code, cancellationToken);
            if (platform is null)
                return ApiResponse<IReadOnlyList<MetaPageDto>>.Fail("Unknown platform.");

            var account = await _unitOfWork.SocialAccounts.GetByUserAndPlatformAsync(userId, platform.Id, cancellationToken);
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
            if (!SupportsPageSelection(code))
                return ApiResponse<SocialAccountDto>.Fail($"Page selection is not available for '{request.PlatformCode}'.");
            if (string.IsNullOrWhiteSpace(request.PageId))
                return ApiResponse<SocialAccountDto>.Fail("Select a page first.");

            var platform = await _unitOfWork.Platforms.GetByCodeAsync(code, cancellationToken);
            if (platform is null)
                return ApiResponse<SocialAccountDto>.Fail("Unknown platform.");

            var account = await _unitOfWork.SocialAccounts.GetByUserAndPlatformAsync(userId, platform.Id, cancellationToken);
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
        CancellationToken cancellationToken = default)
    {
        try
        {
            var code = (platformCode ?? string.Empty).Trim().ToLowerInvariant();
            var platform = await _unitOfWork.Platforms.GetByCodeAsync(code, cancellationToken);
            if (platform is null)
                return ApiResponse<ConnectionDetailsDto>.Fail("Unknown platform.");

            var account = await _unitOfWork.SocialAccounts.GetByUserAndPlatformAsync(userId, platform.Id, cancellationToken);
            if (account is null || account.Status != SocialAccountStatus.Connected)
                return ApiResponse<ConnectionDetailsDto>.Fail($"{platform.Name} is not connected.");

            var profile = account.Profiles.FirstOrDefault();
            var pageId = ResolveSelectedPageId(account, code);
            var isInstagram = code == "instagram";

            var details = new ConnectionDetailsDto
            {
                PlatformCode = platform.Code,
                PlatformName = platform.Name,
                AccountName = account.DisplayName,
                Status = account.Status,
                ConnectedAt = account.ConnectedAt,
                LastSyncAt = account.LastSyncAt,
                PageId = pageId,
                PageName = ReadJsonString(account.MetadataJson, "selectedPageName") ?? profile?.Name,
                PageImage = profile?.ProfileImage,
                InstagramId = isInstagram ? profile?.ExternalProfileId : null,
                InstagramUsername = isInstagram ? profile?.Username : null,
                Profiles = account.Profiles.Select(p => new SocialProfileDto
                {
                    Id = p.Id,
                    ExternalProfileId = p.ExternalProfileId,
                    ProfileType = p.ProfileType.ToString(),
                    Name = p.Name,
                    Username = p.Username
                }).ToList()
            };

            await ApplyWebhookStatusAsync(details, code, pageId, account.Auth?.AccessToken, cancellationToken);
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
            return;

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
        if (!string.IsNullOrWhiteSpace(draft.PageId))
            profile.MetadataJson = JsonSerializer.Serialize(new { pageId = draft.PageId });
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

    private static string? ResolveUserAccessToken(SocialAccount? account)
    {
        var auth = account?.Auth;
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

    public async Task<ApiResponse<object>> DisconnectAsync(Guid userId, string platformCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var platform = await _unitOfWork.Platforms.GetByCodeAsync(platformCode, cancellationToken);
            if (platform is null)
                return ApiResponse<object>.Fail("Unknown platform.");

            var account = await _unitOfWork.SocialAccounts.GetByUserAndPlatformAsync(userId, platform.Id, cancellationToken);
            if (account is null)
                return ApiResponse<object>.Fail("Account not connected.");

            var code = platformCode.Trim().ToLowerInvariant();
            var auth = await _unitOfWork.SocialAuths.GetBySocialAccountIdAsync(account.Id, cancellationToken);

            // Stop Meta from sending webhooks for this page before the token is cleared.
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

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<object>.Ok(new { }, "Account disconnected.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<IReadOnlyList<SocialAccountDto>>> GetConnectedAccountsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var accounts = await _unitOfWork.SocialAccounts.GetByUserAsync(userId, cancellationToken);
            var data = accounts
                .Where(a => a.Status == SocialAccountStatus.Connected)
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
