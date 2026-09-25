using SocialMedia.Application.DTOs.Meta;

namespace SocialMedia.Application.Interfaces;

public interface IMetaApiCallTracker
{
    void Record(
        Guid userId,
        string method,
        string path,
        bool success,
        int? statusCode,
        string? errorCode,
        MetaUsageSnapshotDto? usage);

    MetaApiHealthDto GetHealth(Guid userId);
}
