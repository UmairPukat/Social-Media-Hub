using System.Text.Json;

namespace SocialMedia.Application.Meta;

/// <summary>
/// Detects Meta webhook message echoes (business-side sends reflected back through the webhook).
/// </summary>
public static class MetaWebhookEchoHelper
{
    public static bool IsEcho(JsonElement item, JsonElement message)
    {
        if (HasTrueFlag(message, "is_echo") || HasTrueFlag(item, "is_echo"))
            return true;

        return HasTrueFlag(message, "is_self") || HasTrueFlag(item, "is_self");
    }

    private static bool HasTrueFlag(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(name, out var flag)
           && flag.ValueKind == JsonValueKind.True;
}
