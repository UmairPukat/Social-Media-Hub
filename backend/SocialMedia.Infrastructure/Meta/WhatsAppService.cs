using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Settings;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Interfaces;

namespace SocialMedia.Infrastructure.Meta;

/// <summary>
/// WhatsApp Cloud API. Auth URL is built on the frontend.
/// </summary>
public class WhatsAppService : IWhatsAppService
{
    private readonly MetaGraphClient _graph;
    private readonly WhatsAppSettings _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(MetaGraphClient graph, IOptions<MetaSettings> options, IUnitOfWork unitOfWork, ILogger<WhatsAppService> logger)
    {
        _graph = graph;
        _settings = options.Value.WhatsApp;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OAuthTokenResult> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        using var shortLived = await _graph.GetAsync(
            _settings.GraphApiVersion, "oauth/access_token", string.Empty, cancellationToken,
            ("client_id", _settings.AppId),
            ("client_secret", _settings.AppSecret),
            ("redirect_uri", redirectUri),
            ("code", code));

        var shortToken = shortLived.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Meta did not return an access token.");

        try
        {
            using var longLived = await _graph.GetAsync(
                _settings.GraphApiVersion, "oauth/access_token", string.Empty, cancellationToken,
                ("grant_type", "fb_exchange_token"),
                ("client_id", _settings.AppId),
                ("client_secret", _settings.AppSecret),
                ("fb_exchange_token", shortToken));

            return ParseToken(longLived.RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Long-lived token exchange failed; using short-lived token.");
            return ParseToken(shortLived.RootElement);
        }
    }

    public async Task<(string Id, string Name)> GetMeAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var doc = await _graph.GetAsync(
            _settings.GraphApiVersion, "me", accessToken, cancellationToken,
            ("fields", "id,name"));

        var id = doc.RootElement.GetProperty("id").GetString() ?? string.Empty;
        var name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? "WhatsApp User" : "WhatsApp User";
        return (id, name);
    }

    private static OAuthTokenResult ParseToken(JsonElement root)
    {
        var token = root.GetProperty("access_token").GetString() ?? string.Empty;
        DateTime? expires = null;
        if (root.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var seconds))
            expires = DateTime.UtcNow.AddSeconds(seconds);

        return new OAuthTokenResult
        {
            AccessToken = token,
            ExpiresAt = expires,
            TokenType = root.TryGetProperty("token_type", out var tt) ? tt.GetString() : null
        };
    }

    public Task<IReadOnlyList<SocialProfileDraft>> DiscoverProfilesAsync(
        string userAccessToken, string? phoneNumberId, string? wabaId, CancellationToken cancellationToken = default)
    {
        var id = phoneNumberId;
        if (string.IsNullOrWhiteSpace(id))
            id = _settings.PhoneNumberId;

        if (string.IsNullOrWhiteSpace(id))
            return Task.FromResult<IReadOnlyList<SocialProfileDraft>>(Array.Empty<SocialProfileDraft>());

        IReadOnlyList<SocialProfileDraft> list =
        [
            new SocialProfileDraft
            {
                ExternalProfileId = id,
                Name = "WhatsApp Business",
                ProfileType = "WhatsAppPhone",
                PageAccessToken = userAccessToken
            }
        ];
        return Task.FromResult(list);
    }

    public async Task<string?> SendMessageAsync(MetaCallContext context, string recipientId, string message, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = recipientId,
            type = "text",
            text = new { body = message }
        };
        using var doc = await _graph.PostJsonAsync(_settings.GraphApiVersion, $"{context.ProfileExternalId}/messages", context.AccessToken, payload, cancellationToken);
        if (doc.RootElement.TryGetProperty("messages", out var messages) &&
            messages.ValueKind == JsonValueKind.Array &&
            messages.GetArrayLength() > 0 &&
            messages[0].TryGetProperty("id", out var id))
            return id.GetString();
        return null;
    }

    public async Task DeleteMessageAsync(MetaCallContext context, string messageId, CancellationToken cancellationToken = default)
    {
        var items = await _unitOfWork.Messages.FindAsync(m => m.ExternalMessageId == messageId, cancellationToken);
        foreach (var item in items)
            _unitOfWork.Messages.Remove(item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<WebhookProcessResult> ProcessWebhookPayloadAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        var result = new WebhookProcessResult();
        try
        {
            using var doc = JsonDocument.Parse(webhookEvent.PayloadJson);
            if (!doc.RootElement.TryGetProperty("entry", out var entries))
            {
                result.Skip("Payload has no 'entry' array — not a Meta webhook delivery.");
                return result;
            }

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changes)) continue;
                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var value)) continue;
                    var phoneNumberId = value.TryGetProperty("metadata", out var meta)
                        && meta.TryGetProperty("phone_number_id", out var pn)
                        ? pn.GetString() : null;
                    if (phoneNumberId is null) continue;

                    var profile = await _unitOfWork.SocialProfiles.GetByExternalProfileIdAsync(phoneNumberId, cancellationToken);
                    if (profile is null || !value.TryGetProperty("messages", out var messages)) continue;

                    foreach (var message in messages.EnumerateArray())
                    {
                        var id = message.TryGetProperty("id", out var mid) ? mid.GetString() : null;
                        var from = message.TryGetProperty("from", out var fromEl) ? fromEl.GetString() : null;
                        var text = message.TryGetProperty("text", out var textObj) && textObj.TryGetProperty("body", out var body)
                            ? body.GetString() ?? string.Empty : string.Empty;
                        if (string.IsNullOrWhiteSpace(id)) continue;

                        var conversations = await _unitOfWork.Conversations.FindAsync(
                            c => c.SocialProfileId == profile.Id && c.CustomerId == from, cancellationToken);
                        var conversation = conversations.FirstOrDefault();
                        if (conversation is null)
                        {
                            conversation = new Conversation
                            {
                                SocialProfileId = profile.Id,
                                ExternalConversationId = from ?? id,
                                CustomerId = from,
                                CustomerName = from,
                                LastMessageAt = DateTime.UtcNow,
                                UnreadCount = 1,
                                Status = ConversationStatus.Open
                            };
                            await _unitOfWork.Conversations.AddAsync(conversation, cancellationToken);
                            await _unitOfWork.SaveChangesAsync(cancellationToken);
                        }
                        else
                        {
                            conversation.UnreadCount += 1;
                            conversation.LastMessageAt = DateTime.UtcNow;
                            _unitOfWork.Conversations.Update(conversation);
                        }

                        await _unitOfWork.Messages.AddAsync(new Message
                        {
                            ConversationId = conversation.Id,
                            ExternalMessageId = id,
                            SenderId = from,
                            Direction = MessageDirection.Inbound,
                            MessageType = MessageContentType.Text,
                            Body = text,
                            Status = MessageDeliveryStatus.Delivered,
                            PlatformCreatedAt = DateTime.UtcNow
                        }, cancellationToken);
                        result.Handled++;
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp webhook processing failed for {Id}", webhookEvent.Id);
            throw;
        }
    }
}
