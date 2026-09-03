using SocialMedia.Application.DTOs.Meta;

namespace SocialMedia.Application.Interfaces;

public interface ITikTokService
{
    Task<OAuthTokenResult> ExchangeAuthorizationCodeAsync(
        string clientKey,
        string clientSecret,
        string redirectUri,
        string code,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SocialProfileDraft>> DiscoverProfilesAsync(
        string accessToken,
        CancellationToken cancellationToken = default);
}
