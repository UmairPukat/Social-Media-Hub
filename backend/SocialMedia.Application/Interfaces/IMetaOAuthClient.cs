using SocialMedia.Application.DTOs.Meta;

namespace SocialMedia.Application.Interfaces;

public record MetaOAuthCredentials(
    string AppId,
    string AppSecret,
    string RedirectUri,
    string GraphApiVersion);

public interface IMetaOAuthClient
{
    Task<OAuthTokenResult> ExchangeCodeAsync(
        string platformCode,
        MetaOAuthCredentials credentials,
        string code,
        CancellationToken cancellationToken = default);

    Task<(string Id, string Name)> GetMeAsync(
        string platformCode,
        string graphVersion,
        string accessToken,
        CancellationToken cancellationToken = default);
}
