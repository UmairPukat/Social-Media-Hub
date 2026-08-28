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
            return ContainsRealUserInboundContent(doc.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// True when the payload has a DM or comment shape worth running through inbox processors.
    /// Looser than <see cref="ContainsRealUserInboundContent"/> — echoes/outbound are filtered during processing.
    /// </summary>
    public static bool ShouldProcessForInbox(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (ContainsRealUserInboundContent(doc.RootElement))
                return true;

            return HasInboxCandidatePayload(doc.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Human-readable summary for stored-but-skipped webhook rows.</summary>
    public static string DescribePayload(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty("entry", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
                return "No entry array.";

            var parts = new List<string>();
            foreach (var entry in entries.EnumerateArray())
            {
                var entryId = entry.TryGetProperty("id", out var idElement) ? idElement.ToString() : "?";

                if (entry.TryGetProperty("messaging", out var messaging) && messaging.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in messaging.EnumerateArray())
                    {
                        var kinds = DescribeMessagingItemKinds(item);
                        parts.Add($"entry:{entryId} messaging:[{kinds}]");
                    }
                }

                if (entry.TryGetProperty("changes", out var changes) && changes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var change in changes.EnumerateArray())
                    {
                        var field = change.TryGetProperty("field", out var f) ? f.GetString() : "?";
                        parts.Add($"entry:{entryId} change:{field}");
                    }
                }
            }

            return parts.Count > 0 ? string.Join("; ", parts) : "No messaging/changes fields.";
        }
        catch (JsonException ex)
        {
            return $"Invalid JSON: {ex.Message}";
        }
    }

    private static bool ContainsRealUserInboundContent(JsonElement root)
    {
        if (!root.TryGetProperty("entry", out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var entry in entries.EnumerateArray())
        {
            var entryId = entry.TryGetProperty("id", out var idElement) ? idElement.ToString() : null;

            foreach (var item in EnumerateMessagingItems(entry))
            {
                if (IsRealUserMessageItem(item, entryId))
                    return true;
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

        return false;
    }

    private static bool HasInboxCandidatePayload(JsonElement root)
    {
        if (!root.TryGetProperty("entry", out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var entry in entries.EnumerateArray())
        {
            foreach (var item in EnumerateMessagingItems(entry))
            {
                if (IsInboxMessageCandidate(item))
                    return true;
            }

            if (entry.TryGetProperty("changes", out var changes) &&
                changes.ValueKind == JsonValueKind.Array)
            {
                foreach (var change in changes.EnumerateArray())
                {
                    var field = change.TryGetProperty("field", out var fieldElement) ? fieldElement.GetString() : null;
                    if (field is "comments" or "live_comments")
                        return true;

                    if (field is not ("messages" or "messaging") ||
                        !change.TryGetProperty("value", out var value))
                        continue;

                    if (IsInboxMessageCandidate(value))
                        return true;

                    if (value.TryGetProperty("messaging", out var nested) && nested.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in nested.EnumerateArray())
                        {
                            if (IsInboxMessageCandidate(item))
                                return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    private static bool IsInboxMessageCandidate(JsonElement item)
    {
        if (!item.TryGetProperty("message", out var message))
            return false;

        if (string.IsNullOrWhiteSpace(ReadMessageId(message)))
            return false;

        return !(message.TryGetProperty("is_deleted", out var deleted) && deleted.ValueKind == JsonValueKind.True);
    }

    private static string DescribeMessagingItemKinds(JsonElement item)
    {
        if (item.TryGetProperty("message", out _))
            return "message";

        if (item.TryGetProperty("read", out _))
            return "read";

        if (item.TryGetProperty("delivery", out _))
            return "delivery";

        if (item.TryGetProperty("reaction", out _))
            return "reaction";

        if (item.TryGetProperty("postback", out _))
            return "postback";

        return "other";
    }

    private static IEnumerable<JsonElement> EnumerateMessagingItems(JsonElement entry)
    {
        foreach (var propertyName in new[] { "messaging", "standby" })
        {
            if (!entry.TryGetProperty(propertyName, out var items) ||
                items.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in items.EnumerateArray())
                yield return item;
        }

        if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var change in changes.EnumerateArray())
        {
            var field = change.TryGetProperty("field", out var fieldElement) ? fieldElement.GetString() : null;
            if (field is not ("messages" or "messaging"))
                continue;

            if (!change.TryGetProperty("value", out var value))
                continue;

            if (value.TryGetProperty("message", out _))
                yield return value;

            if (!value.TryGetProperty("messaging", out var nested) ||
                nested.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in nested.EnumerateArray())
                yield return item;
        }
    }

    private static bool IsRealUserChange(JsonElement change, JsonElement value, string? entryId)
    {
        var field = change.TryGetProperty("field", out var fieldElement) ? fieldElement.GetString() : null;

        if (field is "messages" or "messaging")
        {
            if (IsRealUserMessageItem(value, entryId))
                return true;

            if (value.TryGetProperty("messaging", out var nested) && nested.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in nested.EnumerateArray())
                {
                    if (IsRealUserMessageItem(item, entryId))
                        return true;
                }
            }

            return IsRealUserCloudMessagesValue(value, entryId);
        }

        if (field is "comments" or "live_comments")
            return IsRealUserComment(value, entryId);

        if (field is "feed")
        {
            var item = value.TryGetProperty("item", out var itemElement) ? itemElement.GetString() : null;
            return item is "comment" or "reply" && IsRealUserComment(value, entryId);
        }

        if (field is "whatsapp_business_account")
            return IsRealUserCloudMessagesValue(value, entryId);

        return false;
    }

    private static bool IsRealUserDirectField(string? fieldName, JsonElement value, string? entryId)
    {
        if (fieldName is "messages" or "messaging")
        {
            if (IsRealUserMessageItem(value, entryId))
                return true;

            if (value.TryGetProperty("messaging", out var nested) && nested.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in nested.EnumerateArray())
                {
                    if (IsRealUserMessageItem(item, entryId))
                        return true;
                }
            }

            return IsRealUserCloudMessagesValue(value, entryId);
        }

        return fieldName switch
        {
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

        if (MetaWebhookEchoHelper.IsEcho(item, message))
            return false;

        if (message.TryGetProperty("is_deleted", out var deleted) && deleted.ValueKind == JsonValueKind.True)
            return false;

        if (HasMarketingTag(message) || HasMarketingTag(item))
            return false;

        var senderId = ReadActorId(item, "sender") ?? ReadActorId(item, "from");
        var recipientId = ReadActorId(item, "recipient") ?? ReadActorId(item, "to");

        if (!string.IsNullOrWhiteSpace(entryId) &&
            !string.IsNullOrWhiteSpace(senderId) &&
            string.Equals(senderId, entryId, StringComparison.Ordinal))
            return false;

        if (!string.IsNullOrWhiteSpace(entryId) &&
            !string.IsNullOrWhiteSpace(recipientId) &&
            string.Equals(recipientId, entryId, StringComparison.Ordinal))
            return true;

        if (!string.IsNullOrWhiteSpace(senderId) &&
            (string.IsNullOrWhiteSpace(entryId) || !string.Equals(senderId, entryId, StringComparison.Ordinal)))
            return true;

        return string.IsNullOrWhiteSpace(senderId) && string.IsNullOrWhiteSpace(recipientId);
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

    private static bool IsRealUserCloudMessagesValue(JsonElement value, string? entryId)
    {
        if (!value.TryGetProperty("messages", out var messages) ||
            messages.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var message in messages.EnumerateArray())
        {
            if (!message.TryGetProperty("id", out _))
                continue;

            var from = message.TryGetProperty("from", out var fromElement) ? fromElement.ToString() : null;
            if (string.IsNullOrWhiteSpace(from))
                continue;

            if (!string.IsNullOrWhiteSpace(entryId) &&
                string.Equals(from, entryId, StringComparison.Ordinal))
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

        if (actor.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            return actor.ToString();

        return null;
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
