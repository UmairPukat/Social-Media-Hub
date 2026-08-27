using System.Text.Json;
using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Interfaces;

namespace SocialMedia.Infrastructure.Meta;

/// <summary>
/// Resolves the connected profile for a webhook entry, including fallbacks Meta uses in production
/// (recipient id may differ from entry.id on some Instagram/Page deliveries).
/// </summary>
internal static class MetaWebhookEntryHelper
{
    public static async Task<SocialProfile?> ResolveProfileForEntryAsync(
        IUnitOfWork unitOfWork,
        JsonElement entry,
        string? menuType,
        WebhookProcessResult result,
        CancellationToken cancellationToken)
    {
        var entryId = entry.TryGetProperty("id", out var idElement) ? idElement.ToString() : null;
        var tried = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(entryId))
        {
            tried.Add(entryId);
            var profile = await MetaWebhookProfileResolver.TryResolveAsync(
                unitOfWork, entryId, menuType, cancellationToken);
            if (profile is not null)
                return profile;
        }

        foreach (var candidateId in CollectRoutingIds(entry))
        {
            if (string.IsNullOrWhiteSpace(candidateId) || !tried.Add(candidateId))
                continue;

            var profile = await MetaWebhookProfileResolver.TryResolveAsync(
                unitOfWork, candidateId, menuType, cancellationToken);
            if (profile is not null)
                return profile;
        }

        if (WebhookProfileGuard.IsTestDeliveryId(entryId))
        {
            result.Skip($"Test delivery (entry id '{entryId}') ignored — connect a real account to store messages.");
            return null;
        }

        result.Skip(
            $"No connected profile matches entry id '{entryId}' or recipient ids [{string.Join(", ", tried)}]. " +
            "Reconnect in this module and confirm Meta webhook uses the same Page/Instagram id.");
        return null;
    }

    public static IEnumerable<JsonElement> EnumerateMessageArrays(JsonElement entry)
    {
        foreach (var propertyName in new[] { "messaging", "standby" })
        {
            if (entry.TryGetProperty(propertyName, out var items) &&
                items.ValueKind == JsonValueKind.Array)
                yield return items;
        }
    }

    private static IEnumerable<string> CollectRoutingIds(JsonElement entry)
    {
        var ids = new List<string>();

        void Add(string? id)
        {
            if (!string.IsNullOrWhiteSpace(id))
                ids.Add(id);
        }

        foreach (var messages in EnumerateMessageArrays(entry))
        {
            foreach (var item in messages.EnumerateArray())
            {
                if (item.TryGetProperty("recipient", out var recipient) &&
                    recipient.TryGetProperty("id", out var recipientId))
                    Add(recipientId.ToString());

                if (item.TryGetProperty("sender", out var sender) &&
                    sender.TryGetProperty("id", out var senderId))
                    Add(senderId.ToString());
            }
        }

        if (entry.TryGetProperty("changes", out var changes) && changes.ValueKind == JsonValueKind.Array)
        {
            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value))
                    continue;

                if (value.TryGetProperty("recipient", out var recipient) &&
                    recipient.TryGetProperty("id", out var recipientId))
                    Add(recipientId.ToString());

                if (value.TryGetProperty("metadata", out var metadata) &&
                    metadata.TryGetProperty("phone_number_id", out var phoneNumberId))
                    Add(phoneNumberId.ToString());
            }
        }

        return ids.Distinct(StringComparer.Ordinal);
    }
}
