using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers.Common;

public abstract class ProcessWebhooksControllerBase : ControllerBase
{
    private readonly IWebhookService _webhookService;
    private readonly ILogger _logger;

    protected ProcessWebhooksControllerBase(IWebhookService webhookService, ILoggerFactory loggerFactory)
    {
        _webhookService = webhookService;
        _logger = loggerFactory.CreateLogger(GetType());
    }

    protected abstract string MenuType { get; }
    protected abstract string WebhookRoute { get; }

    [HttpGet("webhooks")]
    public async Task<IActionResult> Verify(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.challenge")] string challenge,
        [FromQuery(Name = "hub.verify_token")] string verifyToken)
    {
        var result = await _webhookService.VerifyConnectionForProcessAsync(MenuType, mode, challenge, verifyToken);
        return result is null ? Unauthorized() : Content(result, "text/plain");
    }

    [HttpPost("webhooks")]
    public async Task<IActionResult> Receive()
    {
        var rawBody = await new StreamReader(Request.Body).ReadToEndAsync();
        _logger.LogInformation("{MenuType} webhook received at {Time}", MenuType, DateTime.UtcNow);

        var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        var headersJson = System.Text.Json.JsonSerializer.Serialize(
            Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));

        var resolved = _webhookService.DetectPlatformFromPayload(rawBody) ?? "meta";
        var signatureValid = await _webhookService.IsSignatureValidForProcessAsync(MenuType, resolved, rawBody, signature);
        await _webhookService.ReceiveForProcessAsync(MenuType, resolved, rawBody, signature, headersJson, signatureValid);

        return Ok("EVENT_RECEIVED");
    }

    [Authorize]
    [HttpPost("webhooks/subscribe")]
    public async Task<IActionResult> Subscribe(string platformCode, [FromQuery] string? callbackUrl = null)
    {
        var url = callbackUrl ?? WebhookRoute;
        var response = await _webhookService.SubscribeAsync(platformCode, url);
        return Ok(response);
    }
}
