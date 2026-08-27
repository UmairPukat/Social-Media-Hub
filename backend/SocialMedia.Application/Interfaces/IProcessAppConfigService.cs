using SocialMedia.Application.DTOs.Process;

namespace SocialMedia.Application.Interfaces;

/// <summary>
/// CRUD for per-process Meta app credentials (Integrations, App Connections, Developer Apps).
/// </summary>
public interface IProcessAppConfigService
{
    Task<DTOs.Common.ApiResponse<ProcessAppConfigDto>> GetConfigAsync(
        Guid userId,
        string platformCode,
        string menuType,
        bool revealSecret = false,
        CancellationToken cancellationToken = default);

    Task<DTOs.Common.ApiResponse<ProcessAppConfigDto>> SaveConfigAsync(
        Guid userId,
        SaveProcessAppConfigRequest request,
        CancellationToken cancellationToken = default);

    Task<DTOs.Common.ApiResponse<object>> DeleteConfigAsync(
        Guid userId,
        string platformCode,
        string menuType,
        CancellationToken cancellationToken = default);
}
