using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Extensions;
using SocialMedia.Application.DTOs.Auth;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly IUserAdminService _userAdminService;

    public UsersController(IUserAdminService userAdminService)
    {
        _userAdminService = userAdminService;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var response = await _userAdminService.ListUsersAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPut("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        [FromBody] AdminResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _userAdminService.ResetPasswordAsync(
            User.GetUserId(),
            id,
            request,
            cancellationToken);
        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var response = await _userAdminService.DeleteUserAsync(User.GetUserId(), id, cancellationToken);
        return Ok(response);
    }
}
