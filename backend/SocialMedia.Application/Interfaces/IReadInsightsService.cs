using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Meta;

namespace SocialMedia.Application.Interfaces;

public interface IReadInsightsService
{
    Task<ApiResponse<ReadInsightsDto>> GetAsync(
        Guid userId,
        string menuType,
        int days,
        string? platform,
        CancellationToken cancellationToken = default);
}
