using System.Text.Json;

namespace SocialMedia.Application.Meta;

/// <summary>
/// Detects inbound messages and comments sent by real customers vs business/marketing/social noise.
/// </summary>
public static class MetaWebhookContentClassifier
{
    private static readonly HashSet<string> MarketingMessageTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "MARKETING",
        "ACCOUNT_UPDATE",
        "CONFIRMED_EVENT_UPDATE",
        "POST_PURCHASE_UPDATE",
        "HUMAN_AGENT",
        "CUSTOMER_FEEDBACK"
    };

    /// <summary>
    /// True when the payload contains at least one inbound user DM or user comment (not page echoes, feed posts, or receipts).
    /// </summary>
    public static bool ContainsRealUserInboundContent(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty("entry", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var entry in entries.EnumerateArray())
            {
                var entryId = entry.TryGetProperty("id", out var idElement) ? idElement.ToString() : null;

                if (entry.TryGetProperty("messaging", out var messaging) &&
                    messaging.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in messaging.EnumerateArray())
                    {
                        if (IsRealUserMessageItem(item, entryId))
                            return true;
                    }
                }

                if (entry.TryGetProperty("standby", out var standby) &&
                    standby.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in standby.EnumerateArray())
                    {
                        if (IsRealUserMessageItem(item, entryId))
                            return true;
                    }
                }

                if (entry.TryGetProperty("changes", out var changes) &&
                    changes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var change in changes.EnumerateArray())
                    {
                        if (!change.TryGetProperty("value", out var value))
                            continue;

                        if (IsRealUserChange(change, value, entryId))
                            return true;
                    }
                }

                if (entry.TryGetProperty("field", out var directField) &&
                    entry.TryGetProperty("value", out var directValue))
                {
                    var fieldName = directField.GetString();
                    if (IsRealUserDirectField(fieldName, directValue, entryId))
                        return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool IsRealUserChange(JsonElement change, JsonElement value, string? entryId)
    {
        var field = change.TryGetProperty("field", out var fieldElement) ? fieldElement.GetString() : null;

        if (field is "messages" or "messaging")
            return IsRealUserMessageItem(value, entryId);

        if (field is "comments" or "live_comments")
            return IsRealUserComment(value, entryId);

        if (field is "messages" && value.TryGetProperty("messages", out _))
            return IsRealUserWhatsAppValue(value);

        if (field is "feed")
        {
            var item = value.TryGetProperty("item", out var itemElement) ? itemElement.GetString() : null;
            return item is "comment" or "reply" && IsRealUserComment(value, entryId);
        }

        if (field is "whatsapp_business_account" or null)
            return IsRealUserWhatsAppValue(value);

        return false;
    }

    private static bool IsRealUserDirectField(string? fieldName, JsonElement value, string? entryId)
    {
        return fieldName switch
        {
            "messages" or "messaging" => IsRealUserMessageItem(value, entryId),
            "comments" or "live_comments" => IsRealUserComment(value, entryId),
            _ => false
        };
    }

    private static bool IsRealUserMessageItem(JsonElement item, string? entryId)
    {
        if (!item.TryGetProperty("message", out var message))
            return false;

        if (string.IsNullOrWhiteSpace(ReadMessageId(message)))
            return false;

        if (message.TryGetProperty("is_echo", out var echo) && echo.ValueKind == JsonValueKind.True)
            return false;

        if (message.TryGetProperty("is_deleted", out var deleted) && deleted.ValueKind == JsonValueKind.True)
            return false;

        if (HasMarketingTag(message) || HasMarketingTag(item))
            return false;

        var senderId = ReadActorId(item, "sender");
        if (string.IsNullOrWhiteSpace(senderId))
            return false;

        if (!string.IsNullOrWhiteSpace(entryId) &&
            string.Equals(senderId, entryId, StringComparison.Ordinal))
            return false;

        return true;
    }

    private static bool IsRealUserComment(JsonElement value, string? entryId)
    {
        var commentId = FirstNonEmpty(
            value.TryGetProperty("id", out var id) ? id.ToString() : null,
            value.TryGetProperty("comment_id", out var commentIdElement) ? commentIdElement.ToString() : null);

        if (string.IsNullOrWhiteSpace(commentId))
            return false;

        if (value.TryGetProperty("verb", out var verb))
        {
            var verbValue = verb.GetString();
            if (verbValue is "remove" or "hide" or "unhide" or "edit")
                return false;

            if (verbValue == "add" &&
                value.TryGetProperty("item", out var item) &&
                string.Equals(item.GetString(), "status", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        var authorId = ReadActorId(value, "from");
        if (!string.IsNullOrWhiteSpace(authorId) &&
            !string.IsNullOrWhiteSpace(entryId) &&
            string.Equals(authorId, entryId, StringComparison.Ordinal))
            return false;

        return true;
    }

    private static bool IsRealUserWhatsAppValue(JsonElement value)
    {
        if (!value.TryGetProperty("messages", out var messages) ||
            messages.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var message in messages.EnumerateArray())
        {
            if (!message.TryGetProperty("id", out _))
                continue;

            if (!message.TryGetProperty("from", out _))
                continue;

            if (message.TryGetProperty("type", out var typeElement) &&
                string.Equals(typeElement.GetString(), "reaction", StringComparison.OrdinalIgnoreCase))
                continue;

            return true;
        }

        return false;
    }

    private static bool HasMarketingTag(JsonElement element)
    {
        if (!element.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var tag in tags.EnumerateArray())
        {
            var name = tag.ValueKind == JsonValueKind.String
                ? tag.GetString()
                : tag.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;

            if (!string.IsNullOrWhiteSpace(name) && MarketingMessageTags.Contains(name))
                return true;
        }

        return false;
    }

    private static string? ReadActorId(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var actor))
            return null;

        if (actor.TryGetProperty("id", out var id))
            return id.ToString();

        return actor.ValueKind == JsonValueKind.String ? actor.GetString() : null;
    }

    private static string? ReadMessageId(JsonElement message)
    {
        if (message.TryGetProperty("mid", out var mid) && !string.IsNullOrWhiteSpace(mid.ToString()))
            return mid.ToString();

        if (message.TryGetProperty("message_id", out var messageId) && !string.IsNullOrWhiteSpace(messageId.ToString()))
            return messageId.ToString();

        if (message.TryGetProperty("id", out var id) && !string.IsNullOrWhiteSpace(id.ToString()))
            return id.ToString();

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
