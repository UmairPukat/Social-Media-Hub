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

    private string ResolveRedirectUri(string platformCode, string? fromRequest) =>
        !string.IsNullOrWhiteSpace(fromRequest)
            ? fromRequest!
            : platformCode switch
            {
                "facebook" => _meta.Facebook.RedirectUri,
                "instagram" => string.IsNullOrWhiteSpace(_meta.Instagram.RedirectUri)
                    ? _meta.Facebook.RedirectUri
                    : _meta.Instagram.RedirectUri,
                "whatsapp" => _meta.WhatsApp.RedirectUri,
                _ => string.Empty
            };

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
        _unitOfWork.SocialAccounts.Update(account);

        var auth = await _unitOfWork.SocialAuths.GetBySocialAccountIdAsync(account.Id, cancellationToken);
        if (auth is null)
        {
            auth = new SocialAuth { SocialAccountId = account.Id };
            await _unitOfWork.SocialAuths.AddAsync(auth, cancellationToken);
        }

        auth.AccessToken = accessToken;
        auth.ExpiresAt = expiresAt;
        auth.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.SocialAuths.Update(auth);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var drafts = platformCode.ToLowerInvariant() switch
        {
            "facebook" => await _facebookService.DiscoverProfilesAsync(accessToken, cancellationToken),
            "instagram" => await _instagramService.DiscoverProfilesAsync(accessToken, cancellationToken),
            "whatsapp" => await _whatsAppService.DiscoverProfilesAsync(
                accessToken, _meta.WhatsApp.PhoneNumberId, _meta.WhatsApp.WabaId, cancellationToken),
            _ => Array.Empty<SocialProfileDraft>()
        };

        foreach (var draft in drafts)
        {
            var existing = await _unitOfWork.SocialProfiles.GetByExternalProfileIdAsync(draft.ExternalProfileId, cancellationToken);
            if (existing is null)
            {
                existing = new SocialProfile { SocialAccountId = account.Id };
                await _unitOfWork.SocialProfiles.AddAsync(existing, cancellationToken);
            }

            existing.ExternalProfileId = draft.ExternalProfileId;
            existing.Name = draft.Name;
            existing.Username = draft.Username;
            existing.ProfileImage = draft.ProfileImage;
            existing.ProfileType = ParseProfileType(draft.ProfileType);
            existing.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(draft.PageAccessToken))
            {
                auth.AccessToken = draft.PageAccessToken;
                _unitOfWork.SocialAuths.Update(auth);
            }

            _unitOfWork.SocialProfiles.Update(existing);
        }

        await _unitOfWork.SyncJobs.AddAsync(new SyncJob
        {
            SocialAccountId = account.Id,
            EntityType = SyncEntityType.Posts,
            Status = SyncJobStatus.Pending,
            StartedAt = DateTime.UtcNow
        }, cancellationToken);

        account.LastSyncAt = DateTime.UtcNow;
        _unitOfWork.SocialAccounts.Update(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await _unitOfWork.SocialAccounts.GetWithAuthAndProfilesAsync(account.Id, cancellationToken);
        return ApiResponse<SocialAccountDto>.Ok(MapAccount(reloaded!, platform), "Account connected.");
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

            account.Status = SocialAccountStatus.Disconnected;
            account.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.SocialAccounts.Update(account);

            var auth = await _unitOfWork.SocialAuths.GetBySocialAccountIdAsync(account.Id, cancellationToken);
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
