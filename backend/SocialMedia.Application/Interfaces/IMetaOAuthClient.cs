using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Application.Interfaces;

public record MetaOAuthCredentials(
    string AppId,
    string AppSecret,
    string RedirectUri,
    string GraphApiVersion,
    string BaseUrl);

public interface IMetaOAuthClient
{
    Task<OAuthTokenResult> ExchangeCodeAsync(
        string platformCode,
        MetaOAuthCredentials credentials,
        string code,
        CancellationToken cancellationToken = default);

    Task<(string Id, string Name)> GetMeAsync(
        string platformCode,
        MetaOAuthCredentials credentials,
        string accessToken,
        CancellationToken cancellationToken = default);

    /// <summary>Lists Facebook Pages granted to the user token (me/accounts).</summary>
    Task<IReadOnlyList<MetaPageInfo>> ListPagesAsync(
        string platformCode,
        MetaOAuthCredentials credentials,
        string userAccessToken,
        CancellationToken cancellationToken = default);

    /// <summary>Returns granted scopes for a user token via debug_token, when available.</summary>
    Task<string?> DescribeTokenScopesAsync(
        MetaOAuthCredentials credentials,
        string userAccessToken,
        CancellationToken cancellationToken = default);

    /// <summary>POST {pageId}/subscribed_apps — subscribes the App Connection Meta app to page webhooks.</summary>
    Task SubscribePageWebhooksAsync(
        string platformCode,
        MetaOAuthCredentials credentials,
        MetaPageInfo page,
        CancellationToken cancellationToken = default);

    /// <summary>GET {pageId}/subscribed_apps — webhook fields the page currently sends for this app.</summary>
    Task<IReadOnlyList<string>> GetSubscribedFieldsAsync(
        string platformCode,
        MetaOAuthCredentials credentials,
        string pageId,
        string pageAccessToken,
        CancellationToken cancellationToken = default);
}
