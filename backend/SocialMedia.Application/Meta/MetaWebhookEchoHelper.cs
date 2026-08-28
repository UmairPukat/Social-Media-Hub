using System.Text.Json;

namespace SocialMedia.Application.Meta;

/// <summary>
/// Detects Meta webhook message echoes (business-side sends reflected back through the webhook).
/// </summary>
public static class MetaWebhookEchoHelper
{
    public static bool IsEcho(JsonElement item, JsonElement message)
    {
        if (message.TryGetProperty("is_echo", out var echo) && echo.ValueKind == JsonValueKind.True)
            return true;

        if (item.TryGetProperty("is_echo", out var itemEcho) && itemEcho.ValueKind == JsonValueKind.True)
            return true;

        if (message.TryGetProperty("is_self", out var self) && self.ValueKind == JsonValueKind.True)
            return true;

        return item.TryGetProperty("is_self", out var itemSelf) && itemSelf.ValueKind == JsonValueKind.True;
    }
}
