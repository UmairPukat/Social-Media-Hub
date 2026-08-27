using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Dashboard;

namespace SocialMedia.Application.Interfaces;

/// <summary>
/// Dashboard summary counts.
/// </summary>
public interface IDashboardService
{
    Task<ApiResponse<DashboardSummaryDto>> GetSummaryAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ApiResponse<DashboardSummaryDto>> GetSummaryForProcessAsync(
        Guid userId,
        string? menuType,
        CancellationToken cancellationToken = default);
}
