using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.AppConnection;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Interfaces;

namespace SocialMedia.Application.Services;

public class AppConnectionService : IAppConnectionService
{
    private readonly IUnitOfWork _unitOfWork;

    public AppConnectionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<AppConnectionConfigDto>> GetConfigAsync(
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
            var config = await _unitOfWork.AppConnectionConfigs.GetByUserAndPlatformCodeAsync(
                userId, code, normalizedMenu, cancellationToken);

            if (config is null)
                return ApiResponse<AppConnectionConfigDto>.Fail("No app configuration found for this platform.");

            return ApiResponse<AppConnectionConfigDto>.Ok(Map(config, revealSecret));
        }
        catch (Exception ex)
        {
            return ApiResponse<AppConnectionConfigDto>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<AppConnectionConfigDto>> SaveConfigAsync(
        Guid userId,
        SaveAppConnectionConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var code = (request.PlatformCode ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(code))
                return ApiResponse<AppConnectionConfigDto>.Fail("Platform code is required.");

            if (string.IsNullOrWhiteSpace(request.ClientId))
                return ApiResponse<AppConnectionConfigDto>.Fail("Client Id is required.");

            var normalizedMenu = MenuTypes.Normalize(request.MenuType);
            var platform = await _unitOfWork.Platforms.GetByCodeAsync(code, normalizedMenu, cancellationToken);
            if (platform is null)
                return ApiResponse<AppConnectionConfigDto>.Fail($"Unknown platform '{code}' for menu '{normalizedMenu}'.");

            var config = await _unitOfWork.AppConnectionConfigs.GetByUserAndPlatformAsync(
                userId, platform.Id, normalizedMenu, cancellationToken);

            var isNew = config is null;
            if (config is null)
            {
                if (string.IsNullOrWhiteSpace(request.ClientSecret))
                    return ApiResponse<AppConnectionConfigDto>.Fail("Client Secret is required.");

                config = new AppConnectionConfig
                {
                    UserId = userId,
                    PlatformId = platform.Id,
                    PlatformCode = code,
                    MenuType = normalizedMenu
                };
                await _unitOfWork.AppConnectionConfigs.AddAsync(config, cancellationToken);
            }

            var version = string.IsNullOrWhiteSpace(request.GraphApiVersion)
                ? "v21.0"
                : request.GraphApiVersion.Trim();

            config.Label = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim();
            config.ClientId = request.ClientId.Trim();
            if (!string.IsNullOrWhiteSpace(request.ClientSecret))
                config.ClientSecret = request.ClientSecret.Trim();
            else if (isNew)
                return ApiResponse<AppConnectionConfigDto>.Fail("Client Secret is required.");

            config.RedirectUri = NullIfEmpty(request.RedirectUri);
            config.AuthUrl = NullIfEmpty(request.AuthUrl) ?? PlatformCatalog.DefaultAuthUrl(code, version);
            config.BaseUrl = NullIfEmpty(request.BaseUrl) ?? PlatformCatalog.DefaultBaseUrl(code);
            config.Scopes = NullIfEmpty(request.Scopes) ?? DefaultScopes(code);
            config.GraphApiVersion = version;
            config.WebhookVerifyToken = NullIfEmpty(request.WebhookVerifyToken);
            config.PhoneNumberId = NullIfEmpty(request.PhoneNumberId);
            config.WabaId = NullIfEmpty(request.WabaId);
            config.UpdatedAt = DateTime.UtcNow;

            if (!isNew)
                _unitOfWork.AppConnectionConfigs.Update(config);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<AppConnectionConfigDto>.Ok(Map(config, revealSecret: false), "App configuration saved.");
        }
        catch (Exception ex)
        {
            return ApiResponse<AppConnectionConfigDto>.Fail(ex.Message);
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
            var config = await _unitOfWork.AppConnectionConfigs.GetByUserAndPlatformCodeAsync(
                userId, code, normalizedMenu, cancellationToken);

            if (config is null)
                return ApiResponse<object>.Fail("No app configuration found for this platform.");

            _unitOfWork.AppConnectionConfigs.Remove(config);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<object>.Ok(null!, "App configuration deleted.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    internal static AppConnectionConfigDto Map(AppConnectionConfig config, bool revealSecret)
    {
        return new AppConnectionConfigDto
        {
            Id = config.Id,
            PlatformId = config.PlatformId,
            PlatformCode = config.PlatformCode,
            MenuType = config.MenuType,
            Label = config.Label,
            ClientId = config.ClientId,
            ClientSecret = revealSecret ? config.ClientSecret : MaskSecret(config.ClientSecret),
            HasClientSecret = !string.IsNullOrWhiteSpace(config.ClientSecret),
            RedirectUri = config.RedirectUri,
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
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string MaskSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) return string.Empty;
        if (secret.Length <= 8) return "••••••••";
        return $"{secret[..4]}…{secret[^4..]}";
    }

    private static string DefaultScopes(string platformCode) => platformCode switch
    {
        "facebook" => "public_profile,pages_show_list,pages_read_engagement,pages_read_user_content,pages_manage_engagement,pages_manage_metadata,pages_messaging,business_management",
        "instagram" => "pages_read_user_content,pages_show_list,pages_manage_metadata,pages_messaging,business_management,instagram_basic,instagram_manage_comments,instagram_manage_messages",
        "instagram_login" => "instagram_business_basic,instagram_business_manage_messages,instagram_business_manage_comments",
        "whatsapp" => "whatsapp_business_management,whatsapp_business_messaging,business_management",
        _ => string.Empty
    };
}
