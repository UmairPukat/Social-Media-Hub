using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.TikTok;
using SocialMedia.Application.DTOs.YouTube;

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

    Task<ApiResponse<YouTubePostStatisticsDto>> GetPostStatisticsAsync(
        Guid userId,
        Guid postId,
        string menuType,
        bool refresh = false,
        CancellationToken cancellationToken = default);
}
