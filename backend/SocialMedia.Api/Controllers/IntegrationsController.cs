using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Extensions;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.Integration;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers;

/// <summary>
/// Meta OAuth. Valid OAuth Redirect URI in Meta must be:
/// <c>GET /api/Integrations/Callback</c> on this backend.
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
    /// Authenticated start — builds the Meta Login URL that redirects back to Callback.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> BeginOAuth([FromBody] BeginOAuthRequest model)
    {
        var response = await _integrationService.BeginOAuthAsync(User.GetUserId(), model);
        return Ok(response);
    }

    /// <summary>
    /// Meta Valid OAuth Redirect URI target. Meta redirects the browser here with ?code=&amp;state=.
    /// Returns a tiny HTML page that notifies the opener popup and closes.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code = null,
        [FromQuery] string? state = null,
        [FromQuery] string? error = null,
        [FromQuery(Name = "error_description")] string? errorDescription = null)
    {
        var result = await _integrationService.CompleteMetaRedirectAsync(
            code,
            state,
            errorDescription ?? error);

        return Content(BuildPopupHtml(result), "text/html", Encoding.UTF8);
    }

    /// <summary>Facebook Pages granted by the stored Meta login, shown in the page picker.</summary>
    [HttpGet]
    public async Task<IActionResult> GetPages([FromQuery] string platformCode, [FromQuery] string? menuType = null)
    {
        var response = await _integrationService.GetPagesAsync(
            User.GetUserId(),
            platformCode,
            MenuTypes.Normalize(menuType));
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
    public async Task<IActionResult> GetConnectionDetails([FromQuery] string platformCode, [FromQuery] string? menuType = null)
    {
        var response = await _integrationService.GetConnectionDetailsAsync(
            User.GetUserId(),
            platformCode,
            MenuTypes.Normalize(menuType));
        return Ok(response);
    }

    private static string BuildPopupHtml(MetaRedirectResult result)
    {
        var payload = JsonSerializer.Serialize(new
        {
            type = "smh-meta-oauth",
            platform = result.PlatformCode,
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
              <title>SocialHub Connect</title>
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
