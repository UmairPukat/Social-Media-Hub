using Microsoft.Extensions.Options;
using SocialMedia.Application.DTOs.Auth;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Settings;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Interfaces;

namespace SocialMedia.Application.Services;

/// <summary>
/// Login and invite-token signup. Controllers only call this and return Ok(response).
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IOptions<JwtSettings> jwtOptions)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _jwtSettings = jwtOptions.Value;
    }

    public async Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _unitOfWork.Users.GetByEmailAsync(email, cancellationToken);

            if (user is null || !user.IsActive)
                return ApiResponse<AuthResponse>.Fail("Invalid email or password.");

            if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
                return ApiResponse<AuthResponse>.Fail("Invalid email or password.");

            return ApiResponse<AuthResponse>.Ok(BuildAuthResponse(user), "Login successful.");
        }
        catch (Exception ex)
        {
            return ApiResponse<AuthResponse>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<AuthResponse>> SignupAsync(SignupRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var accessToken = await _unitOfWork.AccessTokens.GetValidTokenAsync(request.AccessToken.Trim(), cancellationToken);
            if (accessToken is null)
                return ApiResponse<AuthResponse>.Fail("Invalid, used, or expired access token.");

            var email = request.Email.Trim().ToLowerInvariant();
            var existingUser = await _unitOfWork.Users.GetByEmailAsync(email, cancellationToken);
            if (existingUser is not null)
                return ApiResponse<AuthResponse>.Fail("An account with this email already exists.");

            var user = new User
            {
                Email = email,
                PasswordHash = _passwordHasher.HashPassword(request.Password),
                FullName = request.FullName.Trim()
            };
            await _unitOfWork.Users.AddAsync(user, cancellationToken);

            accessToken.IsUsed = true;
            accessToken.UsedByUserId = user.Id;
            accessToken.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.AccessTokens.Update(accessToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ApiResponse<AuthResponse>.Ok(BuildAuthResponse(user), "Account created.");
        }
        catch (Exception ex)
        {
            return ApiResponse<AuthResponse>.Fail(ex.Message);
        }
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        return new AuthResponse
        {
            Token = _jwtTokenService.GenerateToken(user),
            Email = user.Email,
            FullName = user.FullName,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes)
        };
    }
}
