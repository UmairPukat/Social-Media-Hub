using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Extensions;
using SocialMedia.Application.DTOs.Integration;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers;

/// <summary>
/// Meta OAuth callbacks only. Frontend opens a Meta popup; Meta redirects with <c>code</c>;
/// these actions exchange the code server-side and connect the account.
/// </summary>
[Authorize]
[Route("api/[controller]/[action]")]
[ApiController]
public class IntegrationsController : ControllerBase
{
    private readonly IIntegrationService _integrationService;

    public IntegrationsController(IIntegrationService integrationService)
    {
        _integrationService = integrationService;
    }

    [HttpPost]
    public async Task<IActionResult> FacebookCallback([FromBody] OAuthCallbackRequest model)
    {
        var response = await _integrationService.FacebookCallbackAsync(User.GetUserId(), model);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> InstagramCallback([FromBody] OAuthCallbackRequest model)
    {
        var response = await _integrationService.InstagramCallbackAsync(User.GetUserId(), model);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> WhatsAppCallback([FromBody] OAuthCallbackRequest model)
    {
        var response = await _integrationService.WhatsAppCallbackAsync(User.GetUserId(), model);
        return Ok(response);
    }

    /// <summary>Facebook Pages granted by the stored Meta login, shown in the page picker.</summary>
    [HttpGet]
    public async Task<IActionResult> GetPages([FromQuery] string platformCode)
    {
        var response = await _integrationService.GetPagesAsync(User.GetUserId(), platformCode);
        return Ok(response);
    }

    /// <summary>Connects the single page the user ticked in the picker.</summary>
    [HttpPost]
    public async Task<IActionResult> SelectPage([FromBody] SelectPageRequest model)
    {
        var response = await _integrationService.SelectPageAsync(User.GetUserId(), model);
        return Ok(response);
    }
}
