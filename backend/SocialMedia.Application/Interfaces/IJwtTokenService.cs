using SocialMedia.Domain.Entities;

namespace SocialMedia.Application.Interfaces;

/// <summary>
/// Creates signed JWT access tokens for authenticated users.
/// The concrete implementation (using System.IdentityModel.Tokens.Jwt) lives in Infrastructure,
/// since token signing is an infrastructure concern, not a business rule.
/// </summary>
public interface IJwtTokenService
{
    string GenerateToken(User user);
}
