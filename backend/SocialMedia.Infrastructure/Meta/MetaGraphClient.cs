using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;

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

    public async Task<MetaGraphHttpResult> GetDetailedAsync(
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

        return new MetaGraphHttpResult(
            JsonDocument.Parse(body),
            ReadUsage(response),
            (int)response.StatusCode);
    }

    private const string PageListFields =
        "id,name,access_token,picture{url},instagram_business_account{id,username,name,profile_picture_url}";
    private const string PageListFieldsBasic = "id,name,access_token";

    /// <summary>
    /// GET me/accounts — the Facebook Pages granted by the user, with any linked Instagram
    /// Business account. Shared by Facebook and Instagram page selection.
    /// Falls back to debug_token granular page ids when Facebook Login for Business
    /// returns an empty accounts list.
    /// </summary>
    public async Task<IReadOnlyList<MetaPageInfo>> ListPagesAsync(
        string version,
        string userAccessToken,
        CancellationToken cancellationToken,
        string? appAccessToken = null)
    {
        string? lastError = null;
        var (pages, accountsError) = await TryListAccountPagesAsync(
            version, userAccessToken, PageListFields, cancellationToken);
        lastError = accountsError;
        if (pages.Count > 0)
            return pages;

        (pages, accountsError) = await TryListAccountPagesAsync(
            version, userAccessToken, PageListFieldsBasic, cancellationToken);
        lastError ??= accountsError;
        if (pages.Count > 0)
            return pages;

        if (!string.IsNullOrWhiteSpace(appAccessToken))
        {
            try
            {
                pages = await ListPagesFromGrantedIdsAsync(
                    version, userAccessToken, appAccessToken, cancellationToken);
                if (pages.Count > 0)
                    return pages;
            }
            catch (Exception ex)
            {
                lastError ??= ex.Message;
            }
        }

        if (!string.IsNullOrWhiteSpace(lastError) && pages.Count == 0)
            throw new InvalidOperationException(lastError);

        return pages;
    }

    private async Task<(List<MetaPageInfo> Pages, string? Error)> TryListAccountPagesAsync(
        string version,
        string userAccessToken,
        string fields,
        CancellationToken cancellationToken)
    {
        var pages = new List<MetaPageInfo>();
        var result = await TryGetFacebookAsync(
            version,
            "me/accounts",
            userAccessToken,
            cancellationToken,
            ("fields", fields),
            ("limit", "100"));

        if (result.Status is < 200 or >= 300)
            return (pages, ReadGraphErrorMessage(result.Body) ?? $"Facebook Pages lookup failed ({result.Status}).");

        AppendPagesFromBody(result.Body, pages);
        await FollowAccountPagesPagingAsync(result.Body, pages, cancellationToken);
        return (pages, null);
    }

    private async Task FollowAccountPagesPagingAsync(
        string body,
        List<MetaPageInfo> pages,
        CancellationToken cancellationToken)
    {
        var next = ReadPagingNext(body);
        var hops = 0;
        while (!string.IsNullOrWhiteSpace(next) && hops++ < 10)
        {
            var pageResult = await TryGetUrlAsync(next, cancellationToken);
            if (pageResult.Status is < 200 or >= 300)
                break;
            AppendPagesFromBody(pageResult.Body, pages);
            next = ReadPagingNext(pageResult.Body);
        }
    }

    private async Task<List<MetaPageInfo>> ListPagesFromGrantedIdsAsync(
        string version,
        string userAccessToken,
        string appAccessToken,
        CancellationToken cancellationToken)
    {
        var pages = new List<MetaPageInfo>();
        var debug = await TryGetFacebookAsync(
            version,
            "debug_token",
            appAccessToken,
            cancellationToken,
            ("input_token", userAccessToken));

        if (debug.Status is < 200 or >= 300)
            throw new InvalidOperationException(ReadGraphErrorMessage(debug.Body) ?? "Could not inspect the Meta login token.");

        foreach (var pageId in ReadGrantedPageIds(debug.Body))
        {
            if (pages.Any(p => p.PageId == pageId))
                continue;

            var pageResult = await TryGetFacebookAsync(
                version,
                pageId,
                userAccessToken,
                cancellationToken,
                ("fields", PageListFields));
            if (pageResult.Status is < 200 or >= 300)
            {
                pageResult = await TryGetFacebookAsync(
                    version,
                    pageId,
                    userAccessToken,
                    cancellationToken,
                    ("fields", PageListFieldsBasic));
            }

            if (pageResult.Status is >= 200 and < 300)
            {
                var parsed = ParsePageNode(pageResult.Body);
                if (parsed is not null)
                    pages.Add(parsed);
            }
            else
            {
                pages.Add(new MetaPageInfo { PageId = pageId, PageName = pageId });
            }
        }

        return pages;
    }

    private static void AppendPagesFromBody(string body, List<MetaPageInfo> pages)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return;

            foreach (var page in data.EnumerateArray())
            {
                var info = ParsePageElement(page);
                if (info is null || pages.Any(p => p.PageId == info.PageId))
                    continue;
                pages.Add(info);
            }
        }
        catch (JsonException)
        {
            // Ignore malformed paging payloads.
        }
    }

    private static MetaPageInfo? ParsePageNode(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return ParsePageElement(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static MetaPageInfo? ParsePageElement(JsonElement page)
    {
        var pageId = page.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty;
        if (string.IsNullOrWhiteSpace(pageId))
            return null;

        var info = new MetaPageInfo
        {
            PageId = pageId,
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

        return info;
    }

    private static IReadOnlyList<string> ReadGrantedPageIds(string debugTokenBody)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            using var doc = JsonDocument.Parse(debugTokenBody);
            if (!doc.RootElement.TryGetProperty("data", out var data))
                return ids.ToList();

            if (data.TryGetProperty("granular_scopes", out var scopes) && scopes.ValueKind == JsonValueKind.Array)
            {
                foreach (var scope in scopes.EnumerateArray())
                {
                    if (!scope.TryGetProperty("target_ids", out var targets) || targets.ValueKind != JsonValueKind.Array)
                        continue;
                    foreach (var target in targets.EnumerateArray())
                    {
                        var value = target.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                            ids.Add(value);
                    }
                }
            }
        }
        catch (JsonException)
        {
            return ids.ToList();
        }

        return ids.ToList();
    }

    private static string? ReadPagingNext(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("paging", out var paging)
                && paging.TryGetProperty("next", out var next)
                && next.ValueKind == JsonValueKind.String)
            {
                return next.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string? ReadGraphErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var message)
                && !string.IsNullOrWhiteSpace(message.GetString()))
            {
                return message.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(body) ? null : body;
    }

    private static string? ReadPictureUrl(JsonElement page) =>
        page.TryGetProperty("picture", out var picture) &&
        picture.TryGetProperty("data", out var pictureData) &&
        pictureData.TryGetProperty("url", out var url)
            ? url.GetString()
            : null;

    public readonly record struct GraphGetResult(int Status, string Body, string Url);

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

    /// <summary>
    /// GET that never throws: returns status and raw body so callers can log Graph payloads
    /// and retry with a Bearer header when the query-string token is rejected.
    /// </summary>
    public Task<GraphGetResult> TryGetFacebookAsync(
        string version,
        string path,
        string accessToken,
        CancellationToken cancellationToken,
        params (string Key, string Value)[] query)
        => TryGetRawAsync(FacebookGraphHost, version, path, accessToken, cancellationToken, query);

    public Task<GraphGetResult> TryGetInstagramAsync(
        string version,
        string path,
        string accessToken,
        CancellationToken cancellationToken,
        params (string Key, string Value)[] query)
        => TryGetRawAsync(InstagramGraphHost, version, path, accessToken, cancellationToken, query);

    public Task<GraphGetResult> TryGetUrlAsync(string url, CancellationToken cancellationToken)
        => SendGetAsync(url, null, cancellationToken);

    private async Task<GraphGetResult> TryGetRawAsync(
        string host,
        string version,
        string path,
        string accessToken,
        CancellationToken cancellationToken,
        params (string Key, string Value)[] query)
    {
        var url = BuildUrl(host, version, path, accessToken, query);
        var first = await SendGetAsync(url, null, cancellationToken);
        if (first.Status is >= 200 and < 300)
            return first;

        var bearerUrl = BuildUrl(host, version, path, string.Empty, query);
        var second = await SendGetAsync(bearerUrl, accessToken, cancellationToken);
        return second.Status is >= 200 and < 300 ? second : first;
    }

    private async Task<GraphGetResult> SendGetAsync(
        string url,
        string? bearerToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(bearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new GraphGetResult((int)response.StatusCode, body, url.Split('?')[0]);
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

    public async Task<MetaGraphHttpResult> PostFormDetailedAsync(
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

        return new MetaGraphHttpResult(
            JsonDocument.Parse(body),
            ReadUsage(response),
            (int)response.StatusCode);
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

    public async Task<MetaGraphHttpResult> PostJsonDetailedAsync(
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

        return new MetaGraphHttpResult(
            JsonDocument.Parse(body),
            ReadUsage(response),
            (int)response.StatusCode);
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

    private static MetaUsageSnapshotDto ReadUsage(HttpResponseMessage response)
    {
        response.Headers.TryGetValues("x-business-use-case-usage", out var bucValues);
        response.Headers.TryGetValues("x-ad-account-usage", out var adValues);
        response.Headers.TryGetValues("x-app-usage", out var appValues);

        return MetaUsageHeaderParser.Parse(
            bucValues?.FirstOrDefault(),
            adValues?.FirstOrDefault(),
            appValues?.FirstOrDefault());
    }
}
