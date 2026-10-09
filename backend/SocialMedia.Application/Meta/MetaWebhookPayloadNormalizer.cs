using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SocialMedia.Application.Meta;

/// <summary>
/// Normalizes Meta webhook envelopes so Instagram Login, Facebook, and WhatsApp processors
/// always see <c>entry[]</c> plus <c>changes[]</c> even when Meta sends a single object
/// or puts <c>field</c>/<c>value</c> directly on the entry.
/// </summary>
public static class MetaWebhookPayloadNormalizer
{
    public static string NormalizeForProcessing(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("entry", out var entries))
            {
                if (entries.ValueKind == JsonValueKind.Object)
                    return RewriteEntries(root);

                if (entries.ValueKind == JsonValueKind.Array &&
                    entries.GetArrayLength() > 0 &&
                    EntriesNeedChangeWrap(entries))
                    return RewriteEntries(root);

                if (entries.ValueKind == JsonValueKind.Array && entries.GetArrayLength() > 0)
                    return payloadJson;
            }

            if (!root.TryGetProperty("field", out var fieldEl) ||
                !root.TryGetProperty("value", out var valueEl))
                return payloadJson;

            var field = fieldEl.GetString();
            if (string.IsNullOrWhiteSpace(field))
                return payloadJson;

            if (valueEl.TryGetProperty("messaging_product", out var product) &&
                string.Equals(product.GetString(), "whatsapp", StringComparison.OrdinalIgnoreCase))
                return WrapRootChange(field, valueEl, "whatsapp_business_account", "0");

            return WrapRootChange(
                field,
                valueEl,
                root.TryGetProperty("object", out var objectEl) ? objectEl.GetString() ?? "instagram" : "instagram",
                ReadActorId(valueEl, "recipient") ?? ReadMediaId(valueEl) ?? string.Empty);
        }
        catch (JsonException)
        {
            return payloadJson;
        }
    }

    public static IEnumerable<JsonElement> EnumerateEntries(JsonElement root)
    {
        if (!root.TryGetProperty("entry", out var entries))
            yield break;

        if (entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in entries.EnumerateArray())
                yield return entry;
            yield break;
        }

        if (entries.ValueKind == JsonValueKind.Object)
            yield return entries;
    }

    public static IEnumerable<JsonElement> EnumerateChanges(JsonElement entry)
    {
        if (entry.TryGetProperty("changes", out var changes))
        {
            if (changes.ValueKind == JsonValueKind.Array)
            {
                foreach (var change in changes.EnumerateArray())
                    yield return change;
            }
            else if (changes.ValueKind == JsonValueKind.Object)
            {
                yield return changes;
            }
        }

        if (entry.TryGetProperty("field", out var field) &&
            entry.TryGetProperty("value", out _) &&
            !string.IsNullOrWhiteSpace(field.GetString()))
        {
            yield return entry;
        }
    }

    public static string? ReadActorId(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var actor))
            return null;

        if (actor.TryGetProperty("id", out var id) && !string.IsNullOrWhiteSpace(id.ToString()))
            return id.ToString();

        if (actor.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var nestedId) && !string.IsNullOrWhiteSpace(nestedId.ToString()))
                    return nestedId.ToString();
                if (item.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                    return item.ToString();
            }
        }

        if (actor.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            return actor.ToString();

        return null;
    }

    /// <summary>
    /// Messenger Platform wraps DMs as <c>{ sender, recipient, message: { mid, text } }</c>.
    /// Instagram Graph v26 (Facebook Login Instagram card) often sends
    /// <c>changes[field=messages].value</c> where <c>message</c> has <c>text</c>/<c>admin_text</c>
    /// and the id lives on the parent, or <c>message</c> is a plain string.
    /// </summary>
    public static bool TryGetMessageEnvelope(JsonElement item, out JsonElement message)
    {
        if (item.TryGetProperty("message", out message) && message.ValueKind == JsonValueKind.Object)
            return true;

        if (item.TryGetProperty("message", out var rawMessage)
            && rawMessage.ValueKind is JsonValueKind.String or JsonValueKind.Number
            && HasMessagingPeer(item))
        {
            message = item;
            return true;
        }

        if (HasInstagramGraphMessageShape(item))
        {
            message = item;
            return true;
        }

        message = default;
        return false;
    }

    public static bool HasMessagingPeer(JsonElement value)
        => ReadActorId(value, "from") is not null
           || ReadActorId(value, "sender") is not null
           || ReadActorId(value, "to") is not null
           || ReadActorId(value, "recipient") is not null;

    public static bool HasInstagramGraphMessageShape(JsonElement value)
    {
        if (value.TryGetProperty("media", out _)
            || value.TryGetProperty("media_id", out _)
            || value.TryGetProperty("comment_id", out _))
            return false;

        if (!HasMessagingPeer(value))
            return false;

        return HasMessageBody(value, value) || !string.IsNullOrWhiteSpace(ReadMessageId(value));
    }

    public static bool HasMessageBody(JsonElement item, JsonElement message)
    {
        if (HasTextProperty(message, "text")
            || HasTextProperty(message, "admin_text")
            || HasTextProperty(item, "text")
            || HasTextProperty(item, "admin_text"))
            return true;

        if (item.TryGetProperty("message", out var raw)
            && raw.ValueKind is JsonValueKind.String or JsonValueKind.Number
            && !string.IsNullOrWhiteSpace(raw.ToString()))
            return true;

        return HasProperty(message, "attachments")
               || HasProperty(item, "attachments")
               || HasProperty(message, "story")
               || HasProperty(item, "story")
               || HasProperty(message, "mid")
               || HasProperty(item, "mid");
    }

    private static bool HasProperty(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out _);

    public static string? ReadMessageId(JsonElement message)
    {
        if (message.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var name in new[] { "mid", "message_id", "id" })
        {
            if (message.TryGetProperty(name, out var id)
                && id.ValueKind is not JsonValueKind.Object and not JsonValueKind.Array
                && !string.IsNullOrWhiteSpace(id.ToString()))
                return id.ToString();
        }

        return null;
    }

    public static string ResolveMessageId(JsonElement item, JsonElement message, string? senderId, string? receiverId)
    {
        var existing = ReadMessageId(message) ?? ReadMessageId(item);
        if (!string.IsNullOrWhiteSpace(existing))
            return existing;

        var text = ReadMessageText(item, message) ?? string.Empty;
        var timestamp = item.TryGetProperty("timestamp", out var ts) ? ts.ToString()
            : item.TryGetProperty("created_time", out var created) ? created.ToString()
            : string.Empty;
        var raw = $"{senderId}|{receiverId}|{timestamp}|{text}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..20].ToLowerInvariant();
        return $"ig-fallback:{hash}";
    }

    public static string? ReadMessageText(JsonElement item, JsonElement message)
    {
        if (message.ValueKind is JsonValueKind.String or JsonValueKind.Number
            && !string.IsNullOrWhiteSpace(message.ToString()))
            return message.ToString();

        foreach (var name in new[] { "text", "admin_text" })
        {
            if (HasTextProperty(message, name, out var nested))
                return nested;
            if (HasTextProperty(item, name, out var flat))
                return flat;
        }

        if (item.TryGetProperty("message", out var raw)
            && raw.ValueKind is JsonValueKind.String or JsonValueKind.Number
            && !string.IsNullOrWhiteSpace(raw.ToString()))
            return raw.ToString();

        if (HasProperty(message, "attachments") || HasProperty(item, "attachments"))
            return "[Instagram attachment]";

        return string.Empty;
    }

    private static bool HasTextProperty(JsonElement element, string name)
        => HasTextProperty(element, name, out _);

    private static bool HasTextProperty(JsonElement element, string name, out string? value)
    {
        value = null;
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var text)
            || text.ValueKind is not (JsonValueKind.String or JsonValueKind.Number)
            || string.IsNullOrWhiteSpace(text.ToString()))
            return false;

        value = text.ToString();
        return true;
    }

    public static string? ReadMediaId(JsonElement value)
    {
        if (value.TryGetProperty("media", out var media))
        {
            if (media.TryGetProperty("id", out var nestedId) && !string.IsNullOrWhiteSpace(nestedId.ToString()))
                return nestedId.ToString();

            if (media.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                return media.ToString();
        }

        if (value.TryGetProperty("media_id", out var mediaId) && !string.IsNullOrWhiteSpace(mediaId.ToString()))
            return mediaId.ToString();

        if (value.TryGetProperty("post_id", out var postId) && !string.IsNullOrWhiteSpace(postId.ToString()))
            return postId.ToString();

        return null;
    }

    public static string? ReadCommentText(JsonElement value)
    {
        if (value.TryGetProperty("text", out var text) &&
            text.ValueKind is JsonValueKind.String or JsonValueKind.Number &&
            !string.IsNullOrWhiteSpace(text.ToString()))
            return text.ToString();

        if (!value.TryGetProperty("message", out var message))
            return null;

        if (message.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            return message.ToString();

        if (message.TryGetProperty("text", out var nested) &&
            nested.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            return nested.ToString();

        return null;
    }

    private static bool EntriesNeedChangeWrap(JsonElement entries)
    {
        foreach (var entry in entries.EnumerateArray())
        {
            var hasFieldValue = entry.TryGetProperty("field", out var field) &&
                                entry.TryGetProperty("value", out _) &&
                                !string.IsNullOrWhiteSpace(field.GetString());
            var hasChangesArray = entry.TryGetProperty("changes", out var changes) &&
                                  changes.ValueKind == JsonValueKind.Array &&
                                  changes.GetArrayLength() > 0;
            if (hasFieldValue && !hasChangesArray)
                return true;

            if (entry.TryGetProperty("changes", out changes) && changes.ValueKind == JsonValueKind.Object)
                return true;
        }

        return false;
    }

    private static string RewriteEntries(JsonElement root)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in root.EnumerateObject())
            {
                if (property.NameEquals("entry"))
                {
                    writer.WritePropertyName("entry");
                    writer.WriteStartArray();
                    foreach (var entry in EnumerateEntries(root))
                        WriteEntry(writer, entry);
                    writer.WriteEndArray();
                    continue;
                }

                property.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteEntry(Utf8JsonWriter writer, JsonElement entry)
    {
        var hasField = entry.TryGetProperty("field", out var field);
        var hasValue = entry.TryGetProperty("value", out var value);
        var hoistField = hasField && hasValue && !string.IsNullOrWhiteSpace(field.GetString());
        var hasChangesArray = entry.TryGetProperty("changes", out var changes) &&
                              changes.ValueKind == JsonValueKind.Array &&
                              changes.GetArrayLength() > 0;
        var wrapObjectChanges = entry.TryGetProperty("changes", out changes) &&
                                changes.ValueKind == JsonValueKind.Object;

        writer.WriteStartObject();
        foreach (var property in entry.EnumerateObject())
        {
            if (hoistField && !hasChangesArray && (property.NameEquals("field") || property.NameEquals("value")))
                continue;
            if (wrapObjectChanges && property.NameEquals("changes"))
                continue;
            property.WriteTo(writer);
        }

        if (wrapObjectChanges)
        {
            writer.WritePropertyName("changes");
            writer.WriteStartArray();
            changes.WriteTo(writer);
            if (hoistField && !hasChangesArray)
                WriteChangeObject(writer, field, value);
            writer.WriteEndArray();
        }
        else if (hoistField && !hasChangesArray)
        {
            writer.WritePropertyName("changes");
            writer.WriteStartArray();
            WriteChangeObject(writer, field, value);
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void WriteChangeObject(Utf8JsonWriter writer, JsonElement field, JsonElement value)
    {
        writer.WriteStartObject();
        writer.WriteString("field", field.GetString());
        writer.WritePropertyName("value");
        value.WriteTo(writer);
        writer.WriteEndObject();
    }

    private static string WrapRootChange(string field, JsonElement valueEl, string objectName, string entryId)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("object", objectName);
            writer.WritePropertyName("entry");
            writer.WriteStartArray();
            writer.WriteStartObject();
            writer.WriteString("id", entryId);
            writer.WritePropertyName("changes");
            writer.WriteStartArray();
            writer.WriteStartObject();
            writer.WriteString("field", field);
            writer.WritePropertyName("value");
            valueEl.WriteTo(writer);
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
