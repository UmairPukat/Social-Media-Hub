using System.Text.Json;

namespace SocialMedia.Application.Meta;

public static class MetaGraphResponseHelper
{
    public static MetaGraphApiException ParseError(int statusCode, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                var message = error.TryGetProperty("message", out var msg) ? msg.GetString() : body;
                var code = error.TryGetProperty("code", out var codeEl) ? codeEl.ToString() : null;
                var type = error.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
                var subcode = error.TryGetProperty("error_subcode", out var sub) ? sub.ToString() : null;
                var combinedCode = subcode is null ? code : $"{code}:{subcode}";
                return new MetaGraphApiException(
                    message ?? "Meta Graph API request failed.",
                    statusCode,
                    combinedCode,
                    message,
                    type);
            }
        }
        catch (JsonException)
        {
            // fall through
        }

        return new MetaGraphApiException(
            $"Meta Graph API request failed ({statusCode}).",
            statusCode,
            metaErrorMessage: body.Length > 500 ? body[..500] : body);
    }

    public static string? ReadNextCursor(JsonElement root)
    {
        if (!root.TryGetProperty("paging", out var paging) ||
            !paging.TryGetProperty("cursors", out var cursors) ||
            !cursors.TryGetProperty("after", out var after))
            return null;

        var value = after.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) ? value.ToString() : null;

    public static string NormalizeAdAccountId(string adAccountId)
    {
        var id = (adAccountId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id))
            throw new MetaGraphApiException("Ad account id is required.");

        return id.StartsWith("act_", StringComparison.OrdinalIgnoreCase) ? id : $"act_{id}";
    }
}
