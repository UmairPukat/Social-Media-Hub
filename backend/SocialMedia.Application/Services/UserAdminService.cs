using SocialMedia.Application.DTOs.Auth;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Interfaces;

namespace SocialMedia.Application.Services;

public class UserAdminService : IUserAdminService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public UserAdminService(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<ApiResponse<IReadOnlyList<UserListItemDto>>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var users = await _unitOfWork.Users.GetAllAsync(cancellationToken);
            var list = users
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new UserListItemDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    FullName = u.FullName,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt
                })
                .ToList();

            return ApiResponse<IReadOnlyList<UserListItemDto>>.Ok(list, "Users loaded.");
        }
        catch (Exception ex)
        {
            return ApiResponse<IReadOnlyList<UserListItemDto>>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<object>> ResetPasswordAsync(
        Guid actorUserId,
        Guid targetUserId,
        AdminResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(targetUserId, cancellationToken);
            if (user is null)
                return ApiResponse<object>.Fail("User not found.");

            user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ApiResponse<object>.Ok(new { userId = user.Id }, "Password updated.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<object>> DeleteUserAsync(
        Guid actorUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (actorUserId == targetUserId)
                return ApiResponse<object>.Fail("You cannot delete your own account.");

            var user = await _unitOfWork.Users.GetByIdAsync(targetUserId, cancellationToken);
            if (user is null)
                return ApiResponse<object>.Fail("User not found.");

            if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                var admins = await _unitOfWork.Users.FindAsync(
                    u => u.Role == "Admin" && u.Id != targetUserId,
                    cancellationToken);
                if (admins.Count == 0)
                    return ApiResponse<object>.Fail("Cannot delete the last admin account.");
            }

            _unitOfWork.Users.Remove(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ApiResponse<object>.Ok(new { userId = targetUserId }, "User deleted.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }
}
