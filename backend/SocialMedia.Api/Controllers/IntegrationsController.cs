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

    /// <summary>
    /// Exchanges a Meta authorization code for tokens and connects the account.
    /// Not Meta's redirect URI — that is the frontend page
    /// <c>/integrations/callback</c>. This API is called by that page after Meta redirects.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Callback([FromBody] OAuthCallbackRequest model)
    {
        var response = await _integrationService.ExchangeAuthCodeAsync(User.GetUserId(), model);
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

    /// <summary>Connected page details and live webhook subscription for the details popup.</summary>
    [HttpGet]
    public async Task<IActionResult> GetConnectionDetails([FromQuery] string platformCode)
    {
        var response = await _integrationService.GetConnectionDetailsAsync(User.GetUserId(), platformCode);
        return Ok(response);
    }
}
