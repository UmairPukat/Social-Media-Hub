using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace SocialMedia.Api.Extensions;

/// <summary>
/// Helpers for reading the authenticated user from JWT claims.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(value) || !Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("User id claim is missing from the token.");

        return userId;
    }
}
