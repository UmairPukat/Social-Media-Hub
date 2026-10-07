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

        if (actor.TryGetProperty("id", out var id))
            return id.ToString();

        if (actor.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            return actor.ToString();

        return null;
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
