using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SocialMedia.Infrastructure.Meta;

/// <summary>
/// Small helper around HttpClient for Meta Graph API calls.
/// Keeps Facebook / Instagram / WhatsApp services free of raw HTTP boilerplate.
/// </summary>
public class MetaGraphClient
{
    private const string FacebookGraphHost = "https://graph.facebook.com";
    private const string InstagramGraphHost = "https://graph.instagram.com";
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

    private async Task<JsonDocument> GetUrlAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Instagram Graph GET failed ({(int)response.StatusCode}): {body}");

        return JsonDocument.Parse(body);
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
