using SocialMedia.Application.DTOs.Meta;

namespace SocialMedia.Application.Interfaces;

public record MetaOAuthCredentials(
    string PlatformCode,
    string Code,
    string RedirectUri,
    string ClientId,
    string ClientSecret,
    string GraphApiVersion,
    string? BaseUrl);

public interface IMetaOAuthExchange
{
    Task<OAuthTokenResult> ExchangeAuthorizationCodeAsync(
        MetaOAuthCredentials credentials,
        CancellationToken cancellationToken = default);
}
