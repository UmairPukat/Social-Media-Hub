using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Extensions;
using SocialMedia.Application.DTOs.Integration;
using SocialMedia.Application.DTOs.Process;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers.Common;

public abstract class ProcessConnectionControllerBase : ControllerBase
{
    private readonly IIntegrationService _integrationService;
    private readonly IProcessAppConfigService _configService;

    protected ProcessConnectionControllerBase(
        IIntegrationService integrationService,
        IProcessAppConfigService configService)
    {
        _integrationService = integrationService;
        _configService = configService;
    }

    protected abstract string MenuType { get; }

    [HttpGet("platforms")]
    public async Task<IActionResult> GetPlatformCards()
    {
        var response = await _integrationService.GetPlatformCardsAsync(User.GetUserId(), MenuType);
        return Ok(response);
    }

    [HttpGet("accounts")]
    public async Task<IActionResult> GetConnectedAccounts()
    {
        var response = await _integrationService.GetConnectedAccountsAsync(User.GetUserId(), MenuType);
        return Ok(response);
    }

    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect([FromQuery] string platformCode)
    {
        var response = await _integrationService.DisconnectAsync(User.GetUserId(), platformCode, MenuType);
        return Ok(response);
    }

    [HttpPost("oauth/begin")]
    public async Task<IActionResult> BeginOAuth([FromBody] BeginOAuthRequest model)
    {
        model.MenuType = MenuType;
        var response = await _integrationService.BeginOAuthAsync(User.GetUserId(), model);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code = null,
        [FromQuery] string? state = null,
        [FromQuery] string? error = null,
        [FromQuery(Name = "error_description")] string? errorDescription = null)
    {
        var result = await _integrationService.CompleteMetaRedirectAsync(code, state, errorDescription ?? error);
        return OAuthPopupHtmlBuilder.AsHtml(result);
    }

    /// <summary>
    /// Meta webhooks must POST to <c>/webhooks</c>, not <c>/callback</c> (OAuth only).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("callback")]
    public IActionResult WebhookMisconfigured()
    {
        var webhooksUrl = Request.Scheme + "://" + Request.Host + Request.Path.Value!.Replace("/callback", "/webhooks", StringComparison.OrdinalIgnoreCase);
        return BadRequest(new
        {
            error = "Wrong URL: Meta webhooks must use the /webhooks endpoint, not /callback.",
            oauthCallback = Request.Path.Value,
            webhookUrl = webhooksUrl,
            hint = "In Meta Developer Console → Webhooks, set Callback URL to the webhookUrl above."
        });
    }

    [HttpGet("pages")]
    public async Task<IActionResult> GetPages([FromQuery] string platformCode)
    {
        var response = await _integrationService.GetPagesAsync(User.GetUserId(), platformCode, MenuType);
        return Ok(response);
    }

    [HttpPost("pages/select")]
    public async Task<IActionResult> SelectPage([FromBody] SelectPageRequest model)
    {
        model.MenuType = MenuType;
        var response = await _integrationService.SelectPageAsync(User.GetUserId(), model);
        return Ok(response);
    }

    [HttpGet("connection-details")]
    public async Task<IActionResult> GetConnectionDetails([FromQuery] string platformCode)
    {
        var response = await _integrationService.GetConnectionDetailsAsync(User.GetUserId(), platformCode, MenuType);
        return Ok(response);
    }

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(
        [FromQuery] string platformCode,
        [FromQuery] bool revealSecret = false)
    {
        var response = await _configService.GetConfigAsync(User.GetUserId(), platformCode, MenuType, revealSecret);
        return Ok(response);
    }

    [HttpPost("config")]
    public async Task<IActionResult> SaveConfig([FromBody] SaveProcessAppConfigRequest model)
    {
        model.MenuType = MenuType;
        var response = await _configService.SaveConfigAsync(User.GetUserId(), model);
        return Ok(response);
    }

    [HttpDelete("config")]
    public async Task<IActionResult> DeleteConfig([FromQuery] string platformCode)
    {
        var response = await _configService.DeleteConfigAsync(User.GetUserId(), platformCode, MenuType);
        return Ok(response);
    }
}
