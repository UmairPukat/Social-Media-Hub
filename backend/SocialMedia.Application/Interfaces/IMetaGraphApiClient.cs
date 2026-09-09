using System.Text.Json;

namespace SocialMedia.Application.Interfaces;

public interface IMetaGraphApiClient
{
    Task<JsonDocument> GetAsync(
        Guid userId,
        string menuType,
        string path,
        CancellationToken cancellationToken,
        params (string Key, string Value)[] query);

    Task<JsonDocument> PostFormAsync(
        Guid userId,
        string menuType,
        string path,
        IDictionary<string, string> formFields,
        CancellationToken cancellationToken);

    Task<JsonDocument> PostJsonAsync(
        Guid userId,
        string menuType,
        string path,
        object payload,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        Guid userId,
        string menuType,
        string path,
        CancellationToken cancellationToken);
}
