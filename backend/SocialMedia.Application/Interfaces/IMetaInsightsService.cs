using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Meta;

namespace SocialMedia.Application.Interfaces;

public interface IMetaInsightsService
{
    Task<ApiResponse<MetaInsightsSummaryDto>> GetInsightsAsync(
        Guid userId,
        string menuType,
        MetaInsightsQuery query,
        CancellationToken cancellationToken = default);
}
