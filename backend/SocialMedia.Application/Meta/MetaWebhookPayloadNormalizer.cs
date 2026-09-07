using System.Text;
using System.Text.Json;

namespace SocialMedia.Application.Meta;

/// <summary>
/// Wraps partial Meta test payloads (e.g. Postman copies of a single "change") into the
/// full webhook envelope the processors expect.
/// </summary>
public static class MetaWebhookPayloadNormalizer
{
    public static string NormalizeForProcessing(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("entry", out var entries) &&
                entries.ValueKind == JsonValueKind.Array &&
                entries.GetArrayLength() > 0)
                return payloadJson;

            if (!root.TryGetProperty("field", out var fieldEl) ||
                !root.TryGetProperty("value", out var valueEl))
                return payloadJson;

            var field = fieldEl.GetString();
            if (string.IsNullOrWhiteSpace(field))
                return payloadJson;

            if (valueEl.TryGetProperty("messaging_product", out var product) &&
                string.Equals(product.GetString(), "whatsapp", StringComparison.OrdinalIgnoreCase))
                return WrapWhatsAppChange(field, valueEl);

            return payloadJson;
        }
        catch (JsonException)
        {
            return payloadJson;
        }
    }

    private static string WrapWhatsAppChange(string field, JsonElement valueEl)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("object", "whatsapp_business_account");
            writer.WritePropertyName("entry");
            writer.WriteStartArray();
            writer.WriteStartObject();
            writer.WriteString("id", "0");
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
