using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.AppConnection;

namespace SocialMedia.Application.Interfaces;

public interface IAppConnectionService
{
    Task<ApiResponse<AppConnectionConfigDto>> GetConfigAsync(
        Guid userId,
        string platformCode,
        string menuType,
        bool revealSecret = false,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AppConnectionConfigDto>> SaveConfigAsync(
        Guid userId,
        SaveAppConnectionConfigRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<object>> DeleteConfigAsync(
        Guid userId,
        string platformCode,
        string menuType,
        CancellationToken cancellationToken = default);
}
