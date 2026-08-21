using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Extensions;
using SocialMedia.Application.DTOs.AppConnections;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers;

/// <summary>
/// User-owned Meta app configurations with separate App Id, secret, and callback URL per connection.
/// </summary>
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
    public async Task<IActionResult> GetAll()
    {
        var response = await _appConnectionService.GetAllAsync(User.GetUserId());
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMetaAppConnectionRequest model)
    {
        var response = await _appConnectionService.CreateAsync(User.GetUserId(), model);
        return Ok(response);
    }

    [HttpPut]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMetaAppConnectionRequest model)
    {
        var response = await _appConnectionService.UpdateAsync(User.GetUserId(), id, model);
        return Ok(response);
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(Guid id)
    {
        var response = await _appConnectionService.DeleteAsync(User.GetUserId(), id);
        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetDefaultScopes([FromQuery] string platformCode)
    {
        var response = await _appConnectionService.GetDefaultScopesAsync(platformCode);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> BeginOAuth([FromBody] BeginAppConnectionOAuthRequest model)
    {
        var response = await _appConnectionService.BeginOAuthAsync(User.GetUserId(), model);
        return Ok(response);
    }

    /// <summary>
    /// Register this URL in each Meta app as Valid OAuth Redirect URI.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code = null,
        [FromQuery] string? state = null,
        [FromQuery] string? error = null,
        [FromQuery(Name = "error_description")] string? errorDescription = null)
    {
        var result = await _appConnectionService.CompleteMetaRedirectAsync(
            code,
            state,
            errorDescription ?? error);

        return Content(BuildPopupHtml(result), "text/html", Encoding.UTF8);
    }

    [HttpGet]
    public async Task<IActionResult> GetPages([FromQuery] Guid appConnectionId)
    {
        var response = await _appConnectionService.GetPagesAsync(User.GetUserId(), appConnectionId);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> SelectPage([FromBody] AppConnectionSelectPageRequest model)
    {
        var response = await _appConnectionService.SelectPageAsync(User.GetUserId(), model);
        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetConnectionDetails([FromQuery] Guid appConnectionId)
    {
        var response = await _appConnectionService.GetConnectionDetailsAsync(User.GetUserId(), appConnectionId);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Disconnect([FromQuery] Guid appConnectionId)
    {
        var response = await _appConnectionService.DisconnectAsync(User.GetUserId(), appConnectionId);
        return Ok(response);
    }

    private static string BuildPopupHtml(AppConnectionMetaRedirectResult result)
    {
        var payload = JsonSerializer.Serialize(new
        {
            type = "smh-app-connection-oauth",
            platform = result.PlatformCode,
            appConnectionId = result.AppConnectionId,
            ok = result.Ok,
            message = result.Message
        });

        var originsJson = JsonSerializer.Serialize(result.FrontendOrigins);
        var statusText = System.Net.WebUtility.HtmlEncode(result.Message);

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <title>SocialHub App Connection</title>
              <style>
                body { font-family: Segoe UI, system-ui, sans-serif; display: grid; place-items: center;
                       min-height: 100vh; margin: 0; background: #f8fafc; color: #1e293b; text-align: center; }
              </style>
            </head>
            <body>
              <p>{{statusText}}</p>
              <script>
                (function () {
                  var payload = {{payload}};
                  var origins = {{originsJson}};
                  if (window.opener && !window.opener.closed) {
                    for (var i = 0; i < origins.length; i++) {
                      try { window.opener.postMessage(payload, origins[i]); } catch (e) {}
                    }
                  }
                  setTimeout(function () { window.close(); }, 700);
                })();
              </script>
            </body>
            </html>
            """;
    }
}
