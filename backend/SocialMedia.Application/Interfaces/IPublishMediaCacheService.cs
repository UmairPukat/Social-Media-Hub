namespace SocialMedia.Application.Interfaces;

/// <summary>
/// Stores uploaded media temporarily so Instagram Login can reference it via a public HTTPS URL.
/// </summary>
public interface IPublishMediaCacheService
{
    Task<string> StoreAsync(
        Stream mediaStream,
        string? fileName,
        string? contentType,
        CancellationToken cancellationToken = default);
}
