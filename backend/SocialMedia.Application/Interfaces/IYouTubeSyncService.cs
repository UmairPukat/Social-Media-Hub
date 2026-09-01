using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.YouTube;

namespace SocialMedia.Application.Interfaces;

public interface IYouTubeSyncService
{
    Task<ApiResponse<YouTubeSyncResultDto>> SyncPostsAsync(
        Guid userId,
        string menuType,
        string? platformCode = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<YouTubeSyncResultDto>> SyncCommentsAsync(
        Guid userId,
        string menuType,
        string? platformCode = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<YouTubeSyncResultDto>> SyncStatisticsAsync(
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
