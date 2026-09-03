using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.TikTok;

namespace SocialMedia.Application.Interfaces;

public interface ITikTokSyncService
{
    Task<ApiResponse<TikTokSyncResultDto>> SyncPostsAsync(
        Guid userId,
        string menuType,
        string? platformCode = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<TikTokSyncResultDto>> SyncStatisticsAsync(
        Guid userId,
        string menuType,
        string? platformCode = null,
        CancellationToken cancellationToken = default);
}
