using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Infrastructure.Media;

public sealed class PublishMediaCacheService : IPublishMediaCacheService
{
    private readonly string _cacheDirectory;
    private readonly string _publicBaseUrl;

    public PublishMediaCacheService(IConfiguration configuration, IHostEnvironment environment)
    {
        _cacheDirectory = Path.Combine(environment.ContentRootPath, "publish-cache");
        Directory.CreateDirectory(_cacheDirectory);

        _publicBaseUrl = FirstNonEmpty(
            configuration["BackendBaseUrl"],
            configuration["backendBaseUrl"])?.TrimEnd('/') ?? string.Empty;
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
