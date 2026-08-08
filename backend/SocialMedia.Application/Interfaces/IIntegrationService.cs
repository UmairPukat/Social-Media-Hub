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

    /// <summary>
    /// Builds the Meta Login URL using the shared backend Callback as redirect_uri.
    /// Put that Callback URL in Meta's Valid OAuth Redirect URIs.
    /// </summary>
    Task<ApiResponse<BeginOAuthResponse>> BeginOAuthAsync(Guid userId, BeginOAuthRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes OAuth after Meta redirects the browser to GET /api/Integrations/Callback.
    /// </summary>
    Task<MetaRedirectResult> CompleteMetaRedirectAsync(string? code, string? state, string? error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges a Meta authorization code for Facebook, Instagram, or WhatsApp (API clients).
    /// </summary>
    Task<ApiResponse<SocialAccountDto>> ExchangeAuthCodeAsync(Guid userId, OAuthCallbackRequest request, CancellationToken cancellationToken = default);

    /// <summary>Facebook Pages granted by the stored user token, for the page picker.</summary>
    Task<ApiResponse<IReadOnlyList<MetaPageDto>>> GetPagesAsync(Guid userId, string platformCode, CancellationToken cancellationToken = default);

    /// <summary>Attaches the single page the user picked and subscribes its webhooks.</summary>
    Task<ApiResponse<SocialAccountDto>> SelectPageAsync(Guid userId, SelectPageRequest request, CancellationToken cancellationToken = default);

    /// <summary>Connected page details plus its live webhook subscription, for the details popup.</summary>
    Task<ApiResponse<ConnectionDetailsDto>> GetConnectionDetailsAsync(Guid userId, string platformCode, CancellationToken cancellationToken = default);
}
