using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Extensions;
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
    public async Task<IActionResult> GetPlatformCards()
    {
        var response = await _integrationService.GetPlatformCardsAsync(User.GetUserId());
        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetConnectedAccounts()
    {
        var response = await _integrationService.GetConnectedAccountsAsync(User.GetUserId());
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Disconnect(string platformCode)
    {
        var response = await _integrationService.DisconnectAsync(User.GetUserId(), platformCode);
        return Ok(response);
    }
}
