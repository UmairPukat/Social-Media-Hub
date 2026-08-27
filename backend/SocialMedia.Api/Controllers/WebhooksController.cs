using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers;

/// <summary>
/// Shared Meta webhook callback for Facebook, Instagram, and WhatsApp.
/// Configure one Callback URL in Meta: <c>GET/POST /api/webhooks</c>.
/// </summary>
[AllowAnonymous]
[Route("api/[controller]/[action]")]
[ApiController]
public class WebhooksController : ControllerBase
{
    private readonly IWebhookService _webhookService;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        IWebhookService webhookService,
        ILogger<WebhooksController> logger)
    {
        _webhookService = webhookService;
        _logger = logger;
    }

    /// <summary>Shared Meta webhook verification for all products.</summary>
    [HttpGet("~/api/webhooks")]
    public IActionResult MetaVerification(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.challenge")] string challenge,
        [FromQuery(Name = "hub.verify_token")] string verifyToken)
    {
        var result = _webhookService.VerifyConnection(null, mode, challenge, verifyToken);
        if (result is null)
            return Unauthorized();

        return Content(result, "text/plain");
    }

    /// <summary>Shared Meta webhook receiver — routes by payload <c>object</c>.</summary>
    [HttpPost("~/api/webhooks")]
    public async Task<IActionResult> MetaEvents()
    {
        var rawBody = await new StreamReader(Request.Body).ReadToEndAsync();

        _logger.LogInformation(
            "META WEBHOOK RECEIVED | Time={Time} | Body={Body}",
            DateTime.UtcNow,
            rawBody);

        if (Request.Headers.TryGetValue("X-Hub-Signature-256", out var receivedSignature))
        {
            _logger.LogInformation(
                "META WEBHOOK SIGNATURE: {Signature}",
                receivedSignature.ToString());
        }

        var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        var headersJson = System.Text.Json.JsonSerializer.Serialize(
            Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));

        var resolved = _webhookService.DetectPlatformFromPayload(rawBody) ?? "meta";
        var signatureValid = _webhookService.IsSignatureValid(resolved, rawBody, signature);
        var response = await _webhookService.ReceiveAsync(resolved, rawBody, signature, headersJson, signatureValid);

        // Always 200 so Meta does not retry or disable the subscription. Signature/process
        // failures are already recorded on WebhookLogs / WebhookEvents.
        return Ok("EVENT_RECEIVED");
    }

    /// <summary>Records webhook subscription settings for a platform (app API, not Meta callback).</summary>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Subscribe(string platformCode, [FromQuery] string? callbackUrl = null)
    {
        var response = await _webhookService.SubscribeAsync(platformCode, callbackUrl);
        return Ok(response);
    }

    /// <summary>
    /// Authenticated test endpoint — posts a Meta-shaped payload without signature checks.
    /// </summary>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> TestReceive(string platformCode = "instagram")
    {
        var payload = await new StreamReader(Request.Body).ReadToEndAsync();
        var headersJson = System.Text.Json.JsonSerializer.Serialize(
            Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));
        var response = await _webhookService.ReceiveAsync(platformCode, payload, "test-signature", headersJson);
        return Ok(response);
    }
}
