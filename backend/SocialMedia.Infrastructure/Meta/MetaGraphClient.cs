using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Infrastructure.Meta;

/// <summary>
/// Small helper around HttpClient for Meta Graph API calls.
/// Keeps Facebook / Instagram / WhatsApp services free of raw HTTP boilerplate.
/// </summary>
public class MetaGraphClient
{
    private const string FacebookGraphHost = "https://graph.facebook.com";
    private const string InstagramGraphHost = "https://graph.instagram.com";

    /// <summary>Webhook fields subscribed on a Facebook Page: feed carries comments; messages for Messenger.</summary>
    public const string PageSubscribedFields = "feed,messages,messaging_postbacks";

    /// <summary>
    /// Page-edge fields used when enabling Instagram via Facebook Login. Instagram <c>comments</c>
    /// itself is subscribed on the Instagram object in the App Dashboard — Meta rejects it on
    /// <c>/{page-id}/subscribed_apps</c>.
    /// </summary>
    public const string InstagramPageSubscribedFields = "feed,messages,messaging_postbacks";

    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MetaGraphClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// GET {base}/{version}/{path}?access_token=...&amp;extra
    /// </summary>
    public async Task<JsonDocument> GetAsync(
        string version,
        string path,
        string accessToken,
        CancellationToken cancellationToken,
        params (string Key, string Value)[] query)
    {
        var url = BuildUrl(FacebookGraphHost, version, path, accessToken, query);
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Meta Graph GET failed ({(int)response.StatusCode}): {body}");

        return JsonDocument.Parse(body);
    }

    /// <summary>
    /// GET me/accounts — the Facebook Pages granted by the user, with any linked Instagram
    /// Business account. Shared by Facebook and Instagram page selection.
    /// </summary>
    public async Task<IReadOnlyList<MetaPageInfo>> ListPagesAsync(
        string version,
        string userAccessToken,
        CancellationToken cancellationToken)
    {
        using var doc = await GetAsync(
            version,
            "me/accounts",
            userAccessToken,
            cancellationToken,
            ("fields", "id,name,access_token,picture{url},instagram_business_account{id,username,name,profile_picture_url}"),
            ("limit", "100"));

        var pages = new List<MetaPageInfo>();
        if (!doc.RootElement.TryGetProperty("data", out var data))
            return pages;

        foreach (var page in data.EnumerateArray())
        {
            var info = new MetaPageInfo
            {
                PageId = page.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                PageName = page.TryGetProperty("name", out var name) ? name.GetString() ?? "Facebook Page" : "Facebook Page",
                PageAccessToken = page.TryGetProperty("access_token", out var token) ? token.GetString() : null,
                PageImage = ReadPictureUrl(page)
            };

            if (page.TryGetProperty("instagram_business_account", out var ig))
            {
                info.InstagramId = ig.TryGetProperty("id", out var igId) ? igId.GetString() : null;
                info.InstagramUsername = ig.TryGetProperty("username", out var igUser) ? igUser.GetString() : null;
                info.InstagramName = ig.TryGetProperty("name", out var igName) ? igName.GetString() : null;
                info.InstagramImage = ig.TryGetProperty("profile_picture_url", out var igPic) ? igPic.GetString() : null;
            }

            if (!string.IsNullOrWhiteSpace(info.PageId))
                pages.Add(info);
        }

        return pages;
    }

    private static string? ReadPictureUrl(JsonElement page) =>
        page.TryGetProperty("picture", out var picture) &&
        picture.TryGetProperty("data", out var pictureData) &&
        pictureData.TryGetProperty("url", out var url)
            ? url.GetString()
            : null;

    public async Task<JsonDocument> GetInstagramAsync(
        string version,
        string path,
        string accessToken,
        CancellationToken cancellationToken,
        params (string Key, string Value)[] query)
    {
        var url = BuildUrl(InstagramGraphHost, version, path, accessToken, query);
        return await GetUrlAsync(url, cancellationToken);
    }

    public async Task<JsonDocument> GetInstagramTokenAsync(
        string path,
        CancellationToken cancellationToken,
        params (string Key, string Value)[] query)
    {
        var url = BuildUrl(InstagramGraphHost, string.Empty, path, string.Empty, query);
        return await GetUrlAsync(url, cancellationToken);
    }

    public async Task<JsonDocument> PostInstagramOAuthAsync(
        IDictionary<string, string> formFields,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(formFields);
        using var response = await _httpClient.PostAsync("https://api.instagram.com/oauth/access_token", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Instagram OAuth failed ({(int)response.StatusCode}): {body}");

        return JsonDocument.Parse(body);
    }

    /// <summary>
    /// POST form or JSON body to Graph API.
    /// </summary>
    public async Task<JsonDocument> PostAsync(
        string version,
        string path,
        string accessToken,
        IDictionary<string, string> formFields,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(FacebookGraphHost, version, path, accessToken);
        using var content = new FormUrlEncodedContent(formFields);
        using var response = await _httpClient.PostAsync(url, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Meta Graph POST failed ({(int)response.StatusCode}): {body}");

        return JsonDocument.Parse(body);
    }

    public async Task<JsonDocument> PostMultipartAsync(
        string version,
        string path,
        string accessToken,
        MultipartFormDataContent content,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(FacebookGraphHost, version, path, accessToken);
        using var response = await _httpClient.PostAsync(url, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Meta Graph multipart POST failed ({(int)response.StatusCode}): {body}");

        return JsonDocument.Parse(body);
    }

    public async Task<JsonDocument> PostInstagramAsync(
        string version,
        string path,
        string accessToken,
        IDictionary<string, string> formFields,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(InstagramGraphHost, version, path, accessToken);
        using var content = new FormUrlEncodedContent(formFields);
        using var response = await _httpClient.PostAsync(url, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Instagram Graph POST failed ({(int)response.StatusCode}): {body}");

        return JsonDocument.Parse(body);
    }

    public async Task<JsonDocument> PostJsonAsync(
        string version,
        string path,
        string accessToken,
        object payload,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(FacebookGraphHost, version, path, accessToken);
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(url, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Meta Graph POST JSON failed ({(int)response.StatusCode}): {body}");

        return JsonDocument.Parse(body);
    }

    public async Task<JsonDocument> PostInstagramJsonAsync(
        string version,
        string path,
        string accessToken,
        object payload,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(InstagramGraphHost, version, path, accessToken);
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(url, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Instagram Graph POST JSON failed ({(int)response.StatusCode}): {body}");

        return JsonDocument.Parse(body);
    }

    public async Task DeleteAsync(
        string version,
        string path,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(FacebookGraphHost, version, path, accessToken);
        using var response = await _httpClient.DeleteAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Meta Graph DELETE failed ({(int)response.StatusCode}): {body}");
    }

    public async Task DeleteInstagramAsync(
        string version,
        string path,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(InstagramGraphHost, version, path, accessToken);
        using var response = await _httpClient.DeleteAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Instagram Graph DELETE failed ({(int)response.StatusCode}): {body}");
    }

    /// <summary>
    /// POST {pageId}/subscribed_apps?subscribed_fields=... — subscribes this app to the page's
    /// webhook fields. The page token goes in the Authorization header, never the query string.
    /// </summary>
    public Task SubscribePageAsync(
        string version,
        string pageId,
        string pageAccessToken,
        string subscribedFields,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(FacebookGraphHost, version, $"{pageId}/subscribed_apps", string.Empty,
            ("subscribed_fields", subscribedFields));
        return SendWithBearerAsync(HttpMethod.Post, url, pageAccessToken, cancellationToken);
    }

    /// <summary>DELETE {pageId}/subscribed_apps — removes this app's page subscription.</summary>
    public Task UnsubscribePageAsync(
        string version,
        string pageId,
        string pageAccessToken,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(FacebookGraphHost, version, $"{pageId}/subscribed_apps", string.Empty);
        return SendWithBearerAsync(HttpMethod.Delete, url, pageAccessToken, cancellationToken);
    }

    /// <summary>GET {pageId}/subscribed_apps — the webhook fields this app currently receives.</summary>
    public async Task<IReadOnlyList<string>> GetPageSubscribedFieldsAsync(
        string version,
        string pageId,
        string pageAccessToken,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(FacebookGraphHost, version, $"{pageId}/subscribed_apps", string.Empty);
        var body = await SendWithBearerAsync(HttpMethod.Get, url, pageAccessToken, cancellationToken);

        var fields = new List<string>();
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data))
            return fields;

        foreach (var app in data.EnumerateArray())
        {
            if (!app.TryGetProperty("subscribed_fields", out var subscribed))
                continue;

            foreach (var field in subscribed.EnumerateArray())
            {
                var name = field.GetString();
                if (!string.IsNullOrWhiteSpace(name) && !fields.Contains(name!))
                    fields.Add(name!);
            }
        }

        return fields;
    }

    private async Task<string> SendWithBearerAsync(
        HttpMethod method,
        string url,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Meta Graph {method} failed ({(int)response.StatusCode}): {body}");

        return body;
    }

    private async Task<JsonDocument> GetUrlAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Instagram Graph GET failed ({(int)response.StatusCode}): {body}");

        return JsonDocument.Parse(body);
    }

    public async Task<JsonDocument> ExchangeOAuthCodeAsync(
        string host,
        string version,
        string clientId,
        string clientSecret,
        string redirectUri,
        string code,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(host, version, "oauth/access_token", string.Empty,
            ("client_id", clientId),
            ("client_secret", clientSecret),
            ("redirect_uri", redirectUri),
            ("code", code));
        return await GetUrlAsync(url, cancellationToken);
    }

    public async Task<JsonDocument> ExchangeLongLivedTokenAsync(
        string host,
        string version,
        string clientId,
        string clientSecret,
        string shortLivedToken,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(host, version, "oauth/access_token", string.Empty,
            ("grant_type", "fb_exchange_token"),
            ("client_id", clientId),
            ("client_secret", clientSecret),
            ("fb_exchange_token", shortLivedToken));
        return await GetUrlAsync(url, cancellationToken);
    }

    private static string BuildUrl(
        string host,
        string version,
        string path,
        string accessToken,
        params (string Key, string Value)[] query)
    {
        var trimmedPath = path.TrimStart('/');
        var versionPrefix = string.IsNullOrWhiteSpace(version) ? string.Empty : $"{version.Trim('/')}/";
        var builder = new StringBuilder($"{host.TrimEnd('/')}/{versionPrefix}{trimmedPath}");

        var first = true;
        void Append(string key, string value)
        {
            builder.Append(first ? '?' : '&');
            first = false;
            builder.Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value));
        }

        // OAuth token exchange calls pass an empty token — skip the query param in that case.
        if (!string.IsNullOrWhiteSpace(accessToken))
            Append("access_token", accessToken);

        foreach (var (key, value) in query)
            Append(key, value);

        return builder.ToString();
    }

    public static T? Deserialize<T>(JsonDocument document)
        => JsonSerializer.Deserialize<T>(document.RootElement.GetRawText(), JsonOptions);
}
