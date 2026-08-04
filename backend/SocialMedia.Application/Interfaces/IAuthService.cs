using SocialMedia.Application.DTOs.Auth;
using SocialMedia.Application.DTOs.Common;

namespace SocialMedia.Application.Interfaces;

/// <summary>
/// Handles login and gated signup.
/// </summary>
public interface IAuthService
{
    Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<AuthResponse>> SignupAsync(SignupRequest request, CancellationToken cancellationToken = default);
}
