using System.Text.Json;
using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Infrastructure.Meta;

/// <summary>
/// Resolves the connected profile for a webhook entry, including fallbacks Meta uses in production
/// (recipient id may differ from entry.id on some Instagram/Page deliveries).
/// </summary>
internal static class MetaWebhookEntryHelper
{
    public static async Task<SocialProfileEntityBase?> ResolveProfileForEntryAsync(
        IProcessDataStore store,
        JsonElement entry,
        WebhookProcessResult result,
        CancellationToken cancellationToken)
    {
        var entryId = entry.TryGetProperty("id", out var idElement) ? idElement.ToString() : null;
        var tried = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(entryId))
        {
            tried.Add(entryId);
            var profile = await MetaWebhookProfileResolver.TryResolveAsync(
                store, entryId, cancellationToken);
            if (profile is not null)
                return profile;
        }

        foreach (var candidateId in CollectRoutingIds(entry))
        {
            if (string.IsNullOrWhiteSpace(candidateId) || !tried.Add(candidateId))
                continue;

            var profile = await MetaWebhookProfileResolver.TryResolveAsync(
                store, candidateId, cancellationToken);
            if (profile is not null)
                return profile;
        }

        var soleMatch = await MetaWebhookProfileResolver.TryResolveSoleConnectedAsync(
            store, cancellationToken);
        if (soleMatch is not null)
            return soleMatch;

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

        foreach (var messages in EnumerateMessageArrays(entry))
        {
            foreach (var item in messages.EnumerateArray())
                CollectActorIds(item, ids);
        }

        foreach (var change in SocialMedia.Application.Meta.MetaWebhookPayloadNormalizer.EnumerateChanges(entry))
        {
            if (!change.TryGetProperty("value", out var value))
                continue;

            CollectActorIds(value, ids);

            if (value.TryGetProperty("messaging", out var nested) && nested.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in nested.EnumerateArray())
                    CollectActorIds(item, ids);
            }
        }

        return ids.Distinct(StringComparer.Ordinal);
    }

    private static void CollectActorIds(JsonElement item, List<string> ids)
    {
        void Add(string? id)
        {
            if (!string.IsNullOrWhiteSpace(id))
                ids.Add(id);
        }

        Add(SocialMedia.Application.Meta.MetaWebhookPayloadNormalizer.ReadActorId(item, "recipient"));
        Add(SocialMedia.Application.Meta.MetaWebhookPayloadNormalizer.ReadActorId(item, "sender"));
        Add(SocialMedia.Application.Meta.MetaWebhookPayloadNormalizer.ReadActorId(item, "to"));
        Add(SocialMedia.Application.Meta.MetaWebhookPayloadNormalizer.ReadActorId(item, "from"));

        if (item.TryGetProperty("metadata", out var metadata) &&
            metadata.TryGetProperty("phone_number_id", out var phoneNumberId))
            Add(phoneNumberId.ToString());
    }
}
