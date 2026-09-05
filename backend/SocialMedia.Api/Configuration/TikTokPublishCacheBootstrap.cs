namespace SocialMedia.Api.Configuration;

/// <summary>
/// Prepares the public <c>/publish-cache</c> folder used by TikTok photo PULL_FROM_URL.
/// </summary>
public static class TikTokPublishCacheBootstrap
{
    public static void EnsurePublishCache(IConfiguration configuration, string publishCachePath, ILogger? logger = null)
    {
        Directory.CreateDirectory(publishCachePath);
        WriteUrlVerificationFile(configuration, publishCachePath, logger);
    }

    public static void LogVerificationStatus(IConfiguration configuration, ILogger logger)
    {
        var fileName = configuration["TikTokSettings:UrlVerificationFileName"]?.Trim();
        var fileContent = configuration["TikTokSettings:UrlVerificationFileContent"];
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(fileContent))
        {
            logger.LogWarning(
                "TikTok URL verification is not configured. Set TikTokSettings__UrlVerificationFileName and TikTokSettings__UrlVerificationFileContent on Railway.");
            return;
        }

        logger.LogInformation(
            "TikTok URL verification ready at /publish-cache/{FileName}",
            Path.GetFileName(fileName));
    }

    /// <summary>
    /// Serves the TikTok verification token directly from configuration so Railway does not depend on disk writes.
    /// </summary>
    public static Func<RequestDelegate, RequestDelegate> CreateVerificationMiddleware(IConfiguration configuration)
    {
        return next => async context =>
        {
            if (HttpMethods.IsGet(context.Request.Method) &&
                context.Request.Path.StartsWithSegments("/publish-cache", out var remainder))
            {
                var requested = remainder.Value?.TrimStart('/');
                if (!string.IsNullOrWhiteSpace(requested) &&
                    !requested.Contains('/', StringComparison.Ordinal))
                {
                    var fileName = configuration["TikTokSettings:UrlVerificationFileName"]?.Trim();
                    var fileContent = configuration["TikTokSettings:UrlVerificationFileContent"];
                    if (!string.IsNullOrWhiteSpace(fileName) &&
                        !string.IsNullOrWhiteSpace(fileContent) &&
                        string.Equals(Path.GetFileName(fileName), requested, StringComparison.Ordinal))
                    {
                        context.Response.ContentType = "text/plain; charset=utf-8";
                        context.Response.Headers.CacheControl = "no-cache";
                        await context.Response.WriteAsync(fileContent.Trim());
                        return;
                    }
                }
            }

            await next(context);
        };
    }

    private static void WriteUrlVerificationFile(
        IConfiguration configuration,
        string publishCachePath,
        ILogger? logger)
    {
        var fileName = configuration["TikTokSettings:UrlVerificationFileName"];
        var fileContent = configuration["TikTokSettings:UrlVerificationFileContent"];
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(fileContent))
            return;

        var safeName = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(safeName))
            return;

        var path = Path.Combine(publishCachePath, safeName);
        File.WriteAllText(path, fileContent.Trim());
        logger?.LogInformation("Wrote TikTok URL verification file to {Path}", path);
    }

    public static string DescribeVerifiedUrlPrefix(IConfiguration configuration)
    {
        var baseUrl = FirstNonEmpty(
            configuration["TikTokSettings:PublishMediaBaseUrl"],
            configuration["BackendBaseUrl"],
            configuration["backendBaseUrl"])?.Trim().TrimEnd('/');

        return string.IsNullOrWhiteSpace(baseUrl)
            ? "https://your-backend-domain/publish-cache/"
            : $"{baseUrl}/publish-cache/";
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
