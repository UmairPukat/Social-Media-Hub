using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.EnvironmentVariables;

namespace SocialMedia.Application.Interfaces;

public interface IEnvironmentVariableService
{
    Task<ApiResponse<IReadOnlyList<EnvironmentVariableDto>>> GetByScopeAsync(string scope, CancellationToken cancellationToken = default);
    Task<ApiResponse<EnvironmentVariableDto>> GetByIdAsync(Guid id, bool revealValue = false, CancellationToken cancellationToken = default);
    Task<ApiResponse<EnvironmentVariableDto>> CreateAsync(UpsertEnvironmentVariableRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<EnvironmentVariableDto>> UpdateAsync(Guid id, UpsertEnvironmentVariableRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
