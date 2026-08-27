using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Extensions;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.AppConnection;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers;

[Authorize]
[Route("api/[controller]/[action]")]
[ApiController]
public class AppConnectionsController : ControllerBase
{
    private readonly IAppConnectionService _appConnectionService;

    public AppConnectionsController(IAppConnectionService appConnectionService)
    {
        _appConnectionService = appConnectionService;
    }

    [HttpGet]
    public async Task<IActionResult> GetConfig(
        [FromQuery] string platformCode,
        [FromQuery] string? menuType = null,
        [FromQuery] bool revealSecret = false)
    {
        var response = await _appConnectionService.GetConfigAsync(
            User.GetUserId(),
            platformCode,
            MenuTypes.Normalize(menuType),
            revealSecret);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> SaveConfig([FromBody] SaveAppConnectionConfigRequest model)
    {
        var response = await _appConnectionService.SaveConfigAsync(User.GetUserId(), model);
        return Ok(response);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteConfig(
        [FromQuery] string platformCode,
        [FromQuery] string? menuType = null)
    {
        var response = await _appConnectionService.DeleteConfigAsync(
            User.GetUserId(),
            platformCode,
            MenuTypes.Normalize(menuType));
        return Ok(response);
    }
}
