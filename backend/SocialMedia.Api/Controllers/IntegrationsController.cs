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
}
