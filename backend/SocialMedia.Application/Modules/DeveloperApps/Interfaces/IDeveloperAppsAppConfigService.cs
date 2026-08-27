using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Process;

namespace SocialMedia.Application.Modules.DeveloperApps.Interfaces;

public interface IDeveloperAppsAppConfigService
{
    Task<ApiResponse<ProcessAppConfigDto>> GetConfigAsync(
        Guid userId,
        string platformCode,
        bool revealSecret = false,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<ProcessAppConfigDto>> SaveConfigAsync(
        Guid userId,
        SaveProcessAppConfigRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<object>> DeleteConfigAsync(
        Guid userId,
        string platformCode,
        CancellationToken cancellationToken = default);
}
