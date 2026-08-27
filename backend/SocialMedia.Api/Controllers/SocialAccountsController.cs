using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Extensions;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers;

/// <summary>
/// Platform cards, connected accounts, and disconnect — separate from OAuth callbacks.
/// </summary>
[Authorize]
[Route("api/[controller]/[action]")]
[ApiController]
public class SocialAccountsController : ControllerBase
{
    private readonly IIntegrationService _integrationService;

    public SocialAccountsController(IIntegrationService integrationService)
    {
        _integrationService = integrationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPlatformCards([FromQuery] string? menuType = null)
    {
        var response = await _integrationService.GetPlatformCardsAsync(
            User.GetUserId(),
            MenuTypes.Normalize(menuType));
        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetConnectedAccounts([FromQuery] string? menuType = null)
    {
        var response = await _integrationService.GetConnectedAccountsAsync(
            User.GetUserId(),
            MenuTypes.Normalize(menuType));
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Disconnect([FromQuery] string platformCode, [FromQuery] string? menuType = null)
    {
        var response = await _integrationService.DisconnectAsync(
            User.GetUserId(),
            platformCode,
            MenuTypes.Normalize(menuType));
        return Ok(response);
    }
}
