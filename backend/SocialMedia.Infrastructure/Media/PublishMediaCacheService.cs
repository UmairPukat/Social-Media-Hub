using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Infrastructure.Media;

public sealed class PublishMediaCacheService : IPublishMediaCacheService
{
    private readonly string _cacheDirectory;
    private readonly string _publicBaseUrl;
    private readonly IHttpClientFactory _httpClientFactory;

    public PublishMediaCacheService(
        IConfiguration configuration,
        IHostEnvironment environment,
        IHttpClientFactory httpClientFactory)
    {
        _cacheDirectory = Path.Combine(environment.ContentRootPath, "publish-cache");
        Directory.CreateDirectory(_cacheDirectory);

        _publicBaseUrl = FirstNonEmpty(
            configuration["BackendBaseUrl"],
            configuration["backendBaseUrl"])?.TrimEnd('/') ?? string.Empty;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> StoreAsync(
        Stream mediaStream,
        string? fileName,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_publicBaseUrl))
            throw new InvalidOperationException("BackendBaseUrl is not configured. Instagram Login needs a public media URL.");

        if (!_publicBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Instagram Login requires BackendBaseUrl to use HTTPS so Meta can fetch uploaded media.");

        var extension = ResolveExtension(fileName, contentType);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(_cacheDirectory, storedName);

        mediaStream.Position = 0;
        await using (var file = File.Create(path))
            await mediaStream.CopyToAsync(file, cancellationToken);

        return $"{_publicBaseUrl}/publish-cache/{storedName}";
    }

    public async Task<string?> StoreFromRemoteAsync(string remoteUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl) || IsLocalCacheUrl(remoteUrl))
            return remoteUrl;

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, remoteUrl);
            request.Headers.TryAddWithoutValidation("User-Agent", "SocialHub/1.0");
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = new MemoryStream();
            await response.Content.CopyToAsync(stream, cancellationToken);
            if (stream.Length == 0)
                return null;

            stream.Position = 0;
            var contentType = response.Content.Headers.ContentType?.MediaType;
            var fileName = Path.GetFileName(new Uri(remoteUrl).AbsolutePath);
            return await StoreInboxAsync(stream, fileName, contentType, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> StoreInboxAsync(
        Stream mediaStream,
        string? fileName,
        string? contentType,
        CancellationToken cancellationToken)
    {
        var extension = ResolveExtension(fileName, contentType);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(_cacheDirectory, storedName);

        mediaStream.Position = 0;
        await using (var file = File.Create(path))
            await mediaStream.CopyToAsync(file, cancellationToken);

        return string.IsNullOrWhiteSpace(_publicBaseUrl)
            ? $"/publish-cache/{storedName}"
            : $"{_publicBaseUrl}/publish-cache/{storedName}";
    }

    public Task<(byte[] Bytes, string ContentType)?> TryReadLocalAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !IsLocalCacheUrl(url))
            return Task.FromResult<(byte[] Bytes, string ContentType)?>(null);

        var name = Path.GetFileName(url.Split('?', 2)[0]);
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult<(byte[] Bytes, string ContentType)?>(null);

        var path = Path.Combine(_cacheDirectory, name);
        if (!File.Exists(path))
            return Task.FromResult<(byte[] Bytes, string ContentType)?>(null);

        var bytes = File.ReadAllBytes(path);
        var contentType = Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".mp4" => "video/mp4",
            _ => "image/jpeg"
        };
        return Task.FromResult<(byte[] Bytes, string ContentType)?>((bytes, contentType));
    }

    private static bool IsLocalCacheUrl(string url)
        => url.Contains("/publish-cache/", StringComparison.OrdinalIgnoreCase);

    private static string ResolveExtension(string? fileName, string? contentType)
    {
        var fromName = Path.GetExtension(fileName ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(fromName))
            return fromName.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(contentType))
            return ".bin";

        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "video/mp4" => ".mp4",
            "video/quicktime" => ".mov",
            "video/webm" => ".webm",
            _ => ".bin"
        };
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
