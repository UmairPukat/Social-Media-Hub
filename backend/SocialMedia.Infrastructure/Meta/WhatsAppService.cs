using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Settings;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Infrastructure.Meta;

/// <summary>
/// WhatsApp Cloud API. Auth URL is built on the frontend.
/// </summary>
public class WhatsAppService : IWhatsAppService
{
    private readonly MetaGraphClient _graph;
    private readonly WhatsAppSettings _settings;
    private readonly IProcessDataStoreFactory _processData;
    private IProcessDataStore? _store;
    private string _menuType = string.Empty;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(
        MetaGraphClient graph,
        IOptions<MetaSettings> options,
        IProcessDataStoreFactory processData,
        ILogger<WhatsAppService> logger)
    {
        _graph = graph;
        _settings = options.Value.WhatsApp;
        _processData = processData;
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

    public async Task<string?> SendMessageAsync(MetaCallContext context, string recipientId, string message, string? replyToMid = null, CancellationToken cancellationToken = default)
    {
        object payload = string.IsNullOrWhiteSpace(replyToMid)
            ? new
            {
                messaging_product = "whatsapp",
                to = recipientId,
                type = "text",
                text = new { body = message }
            }
            : new
            {
                messaging_product = "whatsapp",
                to = recipientId,
                type = "text",
                text = new { body = message },
                context = new { message_id = replyToMid }
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
        foreach (var store in _processData.AllStores())
        {
            var item = await store.GetMessageByExternalIdAsync(messageId, cancellationToken);
            if (item is null)
                continue;

            store.RemoveMessage(item);
            await store.SaveChangesAsync(cancellationToken);
            return;
        }
    }

    public async Task<WebhookProcessResult> ProcessWebhookPayloadAsync(
        WebhookEventEntityBase webhookEvent,
        string menuType,
        CancellationToken cancellationToken = default)
    {
        _store = _processData.ForMenu(menuType);
        _menuType = menuType;
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

                    var profile = await _store.GetProfileByExternalIdAsync(phoneNumberId, cancellationToken);
                    if (profile is null || !value.TryGetProperty("messages", out var messages)) continue;

                    var account = await _store.GetSocialAccountByIdAsync(profile.SocialAccountId, cancellationToken);
                    if (account is null) continue;

                    if (!WebhookProfileGuard.CanProcess(profile, account, _menuType, result))
                        continue;

                    foreach (var message in messages.EnumerateArray())
                    {
                        var id = message.TryGetProperty("id", out var mid) ? mid.GetString() : null;
                        var from = message.TryGetProperty("from", out var fromEl) ? fromEl.GetString() : null;
                        var text = message.TryGetProperty("text", out var textObj) && textObj.TryGetProperty("body", out var body)
                            ? body.GetString() ?? string.Empty : string.Empty;
                        if (string.IsNullOrWhiteSpace(id)) continue;

                        var conversation = await _store.GetConversationByProfileAndCustomerAsync(profile.Id, from ?? id, cancellationToken);
                        if (conversation is null)
                        {
                            conversation = _store.NewConversation();
                            conversation.SocialProfileId = profile.Id;
                            conversation.ExternalConversationId = from ?? id;
                            conversation.CustomerId = from;
                            conversation.CustomerName = from;
                            conversation.LastMessageAt = DateTime.UtcNow;
                            conversation.UnreadCount = 1;
                            conversation.Status = ConversationStatus.Open;
                            await _store.AddConversationAsync(conversation, cancellationToken);
                            await _store.SaveChangesAsync(cancellationToken);
                        }
                        else
                        {
                            conversation.UnreadCount += 1;
                            conversation.LastMessageAt = DateTime.UtcNow;
                            _store.UpdateConversation(conversation);
                        }

                        var row = _store.NewMessage();
                        row.ConversationId = conversation.Id;
                        row.ExternalMessageId = id;
                        row.SenderId = from;
                        row.Direction = MessageDirection.Inbound;
                        row.MessageType = MessageContentType.Text;
                        row.Body = text;
                        row.Status = MessageDeliveryStatus.Delivered;
                        row.PlatformCreatedAt = DateTime.UtcNow;
                        await _store.AddMessageAsync(row, cancellationToken);
                        result.Handled++;
                    }
                }
            }

            await _store.SaveChangesAsync(cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp webhook processing failed for {Id}", webhookEvent.Id);
            throw;
        }
        finally
        {
            _store = null;
            _menuType = string.Empty;
        }
    }
}
