using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Integration;

namespace SocialMedia.Application.Interfaces;

/// <summary>
/// OAuth callbacks (code → token → store account) plus account listing helpers
/// used by SocialAccountsController.
/// </summary>
public interface IIntegrationService
{
    Task<ApiResponse<IReadOnlyList<PlatformCardDto>>> GetPlatformCardsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyList<SocialAccountDto>>> GetConnectedAccountsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ApiResponse<object>> DisconnectAsync(Guid userId, string platformCode, CancellationToken cancellationToken = default);

    /// <summary>Facebook Login popup callback — exchanges code and connects the account.</summary>
    Task<ApiResponse<SocialAccountDto>> FacebookCallbackAsync(Guid userId, OAuthCallbackRequest request, CancellationToken cancellationToken = default);

    /// <summary>Instagram via Facebook Login popup callback.</summary>
    Task<ApiResponse<SocialAccountDto>> InstagramCallbackAsync(Guid userId, OAuthCallbackRequest request, CancellationToken cancellationToken = default);

    /// <summary>WhatsApp Facebook Login popup callback.</summary>
    Task<ApiResponse<SocialAccountDto>> WhatsAppCallbackAsync(Guid userId, OAuthCallbackRequest request, CancellationToken cancellationToken = default);

    /// <summary>Facebook Pages granted by the stored user token, for the page picker.</summary>
    Task<ApiResponse<IReadOnlyList<MetaPageDto>>> GetPagesAsync(Guid userId, string platformCode, CancellationToken cancellationToken = default);

    /// <summary>Attaches the single page the user picked and subscribes its webhooks.</summary>
    Task<ApiResponse<SocialAccountDto>> SelectPageAsync(Guid userId, SelectPageRequest request, CancellationToken cancellationToken = default);

    /// <summary>Connected page details plus its live webhook subscription, for the details popup.</summary>
    Task<ApiResponse<ConnectionDetailsDto>> GetConnectionDetailsAsync(Guid userId, string platformCode, CancellationToken cancellationToken = default);
}
