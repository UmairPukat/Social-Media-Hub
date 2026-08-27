using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Integration;

namespace SocialMedia.Application.Interfaces;

/// <summary>
/// OAuth callbacks (code → token → store account) plus account listing helpers
/// used by SocialAccountsController.
/// </summary>
public interface IIntegrationService
{
    Task<ApiResponse<IReadOnlyList<PlatformCardDto>>> GetPlatformCardsAsync(
        Guid userId,
        string menuType,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyList<SocialAccountDto>>> GetConnectedAccountsAsync(
        Guid userId,
        string menuType,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<object>> DisconnectAsync(
        Guid userId,
        string platformCode,
        string menuType,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<BeginOAuthResponse>> BeginOAuthAsync(
        Guid userId,
        BeginOAuthRequest request,
        CancellationToken cancellationToken = default);

    Task<MetaRedirectResult> CompleteMetaRedirectAsync(
        string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<SocialAccountDto>> ExchangeAuthCodeAsync(
        Guid userId,
        OAuthCallbackRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyList<MetaPageDto>>> GetPagesAsync(
        Guid userId,
        string platformCode,
        string menuType,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<SocialAccountDto>> SelectPageAsync(
        Guid userId,
        SelectPageRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<ConnectionDetailsDto>> GetConnectionDetailsAsync(
        Guid userId,
        string platformCode,
        string menuType,
        CancellationToken cancellationToken = default);
}
