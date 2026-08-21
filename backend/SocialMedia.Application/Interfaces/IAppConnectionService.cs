using SocialMedia.Application.DTOs.AppConnections;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Integration;

namespace SocialMedia.Application.Interfaces;

public interface IAppConnectionService
{
    Task<ApiResponse<IReadOnlyList<MetaAppConnectionDto>>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<MetaAppConnectionDto>> CreateAsync(Guid userId, CreateMetaAppConnectionRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<MetaAppConnectionDto>> UpdateAsync(Guid userId, Guid id, UpdateMetaAppConnectionRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponse<BeginAppConnectionOAuthResponse>> BeginOAuthAsync(Guid userId, BeginAppConnectionOAuthRequest request, CancellationToken cancellationToken = default);
    Task<AppConnectionMetaRedirectResult> CompleteMetaRedirectAsync(string? code, string? state, string? error, CancellationToken cancellationToken = default);
    Task<ApiResponse<IReadOnlyList<MetaPageDto>>> GetPagesAsync(Guid userId, Guid appConnectionId, CancellationToken cancellationToken = default);
    Task<ApiResponse<SocialAccountDto>> SelectPageAsync(Guid userId, AppConnectionSelectPageRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<AppConnectionConnectionDetailsDto>> GetConnectionDetailsAsync(Guid userId, Guid appConnectionId, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> DisconnectAsync(Guid userId, Guid appConnectionId, CancellationToken cancellationToken = default);
    Task<ApiResponse<AppConnectionDefaultScopesDto>> GetDefaultScopesAsync(string platformCode, CancellationToken cancellationToken = default);
}
