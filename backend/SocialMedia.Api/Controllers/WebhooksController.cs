using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers;

/// <summary>
/// Webhook connection (verify), subscribe, and receive only.
/// Auth URLs are handled on the frontend.
/// </summary>
[AllowAnonymous]
[Route("api/[controller]/[action]")]
[ApiController]
public class WebhooksController : ControllerBase
{
    private readonly IWebhookService _webhookService;

    public WebhooksController(IWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    /// <summary>Meta webhook verification (hub.mode / hub.challenge / hub.verify_token).</summary>
    [HttpGet]
    public IActionResult Connection(
        string platformCode,
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.challenge")] string challenge,
        [FromQuery(Name = "hub.verify_token")] string verifyToken)
    {
        var result = _webhookService.VerifyConnection(platformCode, mode, challenge, verifyToken);
        if (result is null)
            return Unauthorized();

        return Content(result, "text/plain");
    }

    /// <summary>Records webhook subscription settings for a platform.</summary>
    [HttpPost]
    public async Task<IActionResult> Subscribe(string platformCode, [FromQuery] string? callbackUrl = null)
    {
        var response = await _webhookService.SubscribeAsync(platformCode, callbackUrl);
        return Ok(response);
    }

    /// <summary>Receives webhook payloads — always saves WebhookEvent first.</summary>
    [HttpPost]
    public async Task<IActionResult> Received(string platformCode)
        => await ReceivePlatformAsync(platformCode);

    /// <summary>Single callback URL used in Meta's Instagram webhook configuration.</summary>
    [HttpGet("~/api/webhooks/instagram")]
    public IActionResult InstagramVerification(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.challenge")] string challenge,
        [FromQuery(Name = "hub.verify_token")] string verifyToken)
        => Connection("instagram", mode, challenge, verifyToken);

    [HttpPost("~/api/webhooks/instagram")]
    public Task<IActionResult> InstagramEvents()
        => ReceivePlatformAsync("instagram");

    /// <summary>
    /// Authenticated test endpoint — posts a Meta-shaped payload without signature checks.
    /// Use this to verify WebhookLogs + comment/message + SignalR before live Meta events.
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

    private async Task<IActionResult> ReceivePlatformAsync(string platformCode)
    {
        var payload = await new StreamReader(Request.Body).ReadToEndAsync();
        var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        if (!_webhookService.IsSignatureValid(platformCode, payload, signature))
            return Unauthorized();

        var headersJson = System.Text.Json.JsonSerializer.Serialize(
            Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));

        var response = await _webhookService.ReceiveAsync(platformCode, payload, signature, headersJson);
        return Ok(response);
    }
}
