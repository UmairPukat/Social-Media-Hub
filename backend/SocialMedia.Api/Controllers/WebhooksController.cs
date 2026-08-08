using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers;

/// <summary>
/// Shared Meta webhook callback for Facebook, Instagram, and WhatsApp.
/// Configure one Callback URL in Meta: <c>GET/POST /api/webhooks</c>.
/// Platform-specific aliases remain for older Meta product settings.
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

    /// <summary>Shared Meta webhook verification for all products.</summary>
    [HttpGet("~/api/webhooks")]
    public IActionResult MetaVerification(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.challenge")] string challenge,
        [FromQuery(Name = "hub.verify_token")] string verifyToken)
        => Connection(null, mode, challenge, verifyToken);

    /// <summary>Shared Meta webhook receiver — routes by payload <c>object</c>.</summary>
    [HttpPost("~/api/webhooks")]
    public Task<IActionResult> MetaEvents()
        => ReceivePlatformAsync(null);

    /// <summary>Meta webhook verification (hub.mode / hub.challenge / hub.verify_token).</summary>
    [HttpGet]
    public IActionResult Connection(
        string? platformCode,
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
    public async Task<IActionResult> Received(string? platformCode = null)
        => await ReceivePlatformAsync(platformCode);

    /// <summary>Legacy Instagram callback alias.</summary>
    [HttpGet("~/api/webhooks/instagram")]
    public IActionResult InstagramVerification(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.challenge")] string challenge,
        [FromQuery(Name = "hub.verify_token")] string verifyToken)
        => Connection("instagram", mode, challenge, verifyToken);

    [HttpPost("~/api/webhooks/instagram")]
    public Task<IActionResult> InstagramEvents()
        => ReceivePlatformAsync("instagram");

    /// <summary>Legacy Facebook callback alias.</summary>
    [HttpGet("~/api/webhooks/facebook")]
    public IActionResult FacebookVerification(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.challenge")] string challenge,
        [FromQuery(Name = "hub.verify_token")] string verifyToken)
        => Connection("facebook", mode, challenge, verifyToken);

    [HttpPost("~/api/webhooks/facebook")]
    public Task<IActionResult> FacebookEvents()
        => ReceivePlatformAsync("facebook");

    /// <summary>Legacy WhatsApp callback alias.</summary>
    [HttpGet("~/api/webhooks/whatsapp")]
    public IActionResult WhatsAppVerification(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.challenge")] string challenge,
        [FromQuery(Name = "hub.verify_token")] string verifyToken)
        => Connection("whatsapp", mode, challenge, verifyToken);

    [HttpPost("~/api/webhooks/whatsapp")]
    public Task<IActionResult> WhatsAppEvents()
        => ReceivePlatformAsync("whatsapp");

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

    private async Task<IActionResult> ReceivePlatformAsync(string? platformCode)
    {
        var payload = await new StreamReader(Request.Body).ReadToEndAsync();
        var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        var headersJson = System.Text.Json.JsonSerializer.Serialize(
            Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));

        var resolved = _webhookService.DetectPlatformFromPayload(payload) ?? platformCode ?? "meta";
        var signatureValid = _webhookService.IsSignatureValid(resolved, payload, signature);
        var response = await _webhookService.ReceiveAsync(resolved, payload, signature, headersJson, signatureValid);

        return signatureValid ? Ok(response) : Unauthorized(response);
    }
}
