using System.Text.Json;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Infrastructure.Meta;

internal static class MetaMessagingHelper
{
    public static string? ReadMessageId(JsonElement message)
    {
        if (message.TryGetProperty("mid", out var mid) && !string.IsNullOrWhiteSpace(mid.ToString()))
            return mid.ToString();

        if (message.TryGetProperty("message_id", out var messageId) && !string.IsNullOrWhiteSpace(messageId.ToString()))
            return messageId.ToString();

        if (message.TryGetProperty("id", out var id) && !string.IsNullOrWhiteSpace(id.ToString()))
            return id.ToString();

        return null;
    }

    public static bool ProfileOwnsSenderId(SocialProfileEntityBase profile, string? senderId)
    {
        if (string.IsNullOrWhiteSpace(senderId))
            return false;

        if (senderId == profile.ExternalProfileId)
            return true;

        if (TryReadMetadataString(profile.MetadataJson, "pageId") is { } pageId &&
            string.Equals(pageId, senderId, StringComparison.Ordinal))
            return true;

        return ReadAlternateIds(profile.MetadataJson).Contains(senderId);
    }

    /// <summary>True when the payload looks like a user DM/comment, not a read/delivery receipt.</summary>
    public static bool PayloadContainsUserContent(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty("entry", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.TryGetProperty("messaging", out var messaging) &&
                    messaging.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in messaging.EnumerateArray())
                    {
                        if (item.TryGetProperty("message", out var message) &&
                            !string.IsNullOrWhiteSpace(ReadMessageId(message)))
                            return true;
                    }
                }

                if (entry.TryGetProperty("changes", out var changes) &&
                    changes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var change in changes.EnumerateArray())
                    {
                        var field = change.TryGetProperty("field", out var fieldElement)
                            ? fieldElement.GetString()
                            : null;
                        if (field is "messages" or "comments" or "live_comments" or "messaging")
                            return true;
                    }
                }

                if (entry.TryGetProperty("field", out var directField) &&
                    entry.TryGetProperty("value", out var directValue))
                {
                    var fieldName = directField.GetString();
                    if (fieldName is "messages" or "comments" or "live_comments")
                        return true;

                    if (directValue.TryGetProperty("message", out var directMessage) &&
                        !string.IsNullOrWhiteSpace(ReadMessageId(directMessage)))
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

    private static string? TryReadMetadataString(string? metadataJson, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            return doc.RootElement.TryGetProperty(propertyName, out var value)
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ReadAlternateIds(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return Array.Empty<string>();

        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            if (!doc.RootElement.TryGetProperty("alternateIds", out var ids) ||
                ids.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();

            return ids.EnumerateArray()
                .Select(id => id.ToString())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
