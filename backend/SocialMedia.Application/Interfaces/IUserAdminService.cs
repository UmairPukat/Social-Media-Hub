using SocialMedia.Application.DTOs.Auth;
using SocialMedia.Application.DTOs.Common;

namespace SocialMedia.Application.Interfaces;

public interface IUserAdminService
{
    Task<ApiResponse<IReadOnlyList<UserListItemDto>>> ListUsersAsync(CancellationToken cancellationToken = default);

    Task<ApiResponse<object>> ResetPasswordAsync(
        Guid actorUserId,
        Guid targetUserId,
        AdminResetPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<object>> DeleteUserAsync(
        Guid actorUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default);
}
