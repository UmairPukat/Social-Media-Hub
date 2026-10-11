using System.Text.Json;
using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Application.Meta;

/// <summary>
/// Meta delivers every app to one callback URL. Route inbox persistence to the
/// module that actually owns the connected Page / Instagram account.
/// </summary>
public static class WebhookInboxRouter
{
    public static async Task<IReadOnlyList<string>> ResolveOwningMenusAsync(
        IProcessDataStoreFactory processData,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var candidateIds = CollectCandidateIds(payloadJson);
        if (candidateIds.Count == 0)
            return Array.Empty<string>();

        var owners = new List<string>();
        foreach (var store in processData.AllStores())
        {
            if (await StoreOwnsAnyAsync(store, candidateIds, cancellationToken))
                owners.Add(store.MenuType);
        }

        if (owners.Count == 0 && IsInstagramDelivery(payloadJson))
        {
            foreach (var store in processData.AllStores())
            {
                if (await StoreHasConnectedInstagramAsync(store, cancellationToken))
                    owners.Add(store.MenuType);
            }
        }

        return owners;
    }

    public static IReadOnlyList<string> MenusToProcess(string incomingMenu, IReadOnlyList<string> owners)
        => owners.Count > 0 ? owners : [incomingMenu];

    private static async Task<bool> StoreOwnsAnyAsync(
        IProcessDataStore store,
        IReadOnlyList<string> candidateIds,
        CancellationToken cancellationToken)
    {
        foreach (var id in candidateIds)
        {
            var profiles = await store.FindProfilesByExternalIdAsync(id, cancellationToken);
            foreach (var profile in profiles)
            {
                var account = await store.GetSocialAccountByIdAsync(profile.SocialAccountId, cancellationToken);
                if (account?.Status == SocialAccountStatus.Connected)
                    return true;
            }

            var byAccount = await store.GetSocialAccountByExternalIdAsync(id, cancellationToken);
            if (byAccount?.Status == SocialAccountStatus.Connected)
                return true;
        }

        var connected = await store.FindConnectedSocialAccountsAsync(cancellationToken);
        foreach (var account in connected)
        {
            var profiles = await store.GetProfilesByAccountAsync(account.Id, cancellationToken);
            foreach (var profile in profiles)
            {
                foreach (var id in candidateIds)
                {
                    if (InboxRoutingHelper.MatchesConnectedIdentity(profile, account, id))
                        return true;
                }
            }
        }

        return false;
    }

    private static async Task<bool> StoreHasConnectedInstagramAsync(
        IProcessDataStore store,
        CancellationToken cancellationToken)
    {
        var instagram = await store.GetPlatformByCodeAsync("instagram", cancellationToken);
        var instagramLogin = await store.GetPlatformByCodeAsync("instagram_login", cancellationToken);
        var accounts = await store.FindConnectedSocialAccountsAsync(cancellationToken);
        return accounts.Any(account =>
            (instagram is not null && account.PlatformId == instagram.Id)
            || (instagramLogin is not null && account.PlatformId == instagramLogin.Id));
    }

    private static bool IsInstagramDelivery(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("object", out var objectElement)
                && string.Equals(objectElement.GetString(), "instagram", StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var entry in MetaWebhookPayloadNormalizer.EnumerateEntries(doc.RootElement))
            {
                if (entry.TryGetProperty("messaging", out var messaging)
                    && messaging.ValueKind == JsonValueKind.Array
                    && messaging.GetArrayLength() > 0)
                    return true;
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }

    private static IReadOnlyList<string> CollectCandidateIds(string payloadJson)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            foreach (var entry in MetaWebhookPayloadNormalizer.EnumerateEntries(doc.RootElement))
            {
                Add(ids, entry.TryGetProperty("id", out var entryId) ? entryId.ToString() : null);

                foreach (var propertyName in new[] { "messaging", "standby" })
                {
                    if (!entry.TryGetProperty(propertyName, out var items) || items.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var item in items.EnumerateArray())
                    {
                        Add(ids, MetaWebhookPayloadNormalizer.ReadActorId(item, "recipient"));
                        Add(ids, MetaWebhookPayloadNormalizer.ReadActorId(item, "to"));
                    }
                }

                foreach (var change in MetaWebhookPayloadNormalizer.EnumerateChanges(entry))
                {
                    if (!change.TryGetProperty("value", out var value))
                        continue;

                    Add(ids, MetaWebhookPayloadNormalizer.ReadActorId(value, "recipient"));
                    Add(ids, MetaWebhookPayloadNormalizer.ReadActorId(value, "to"));
                    if (value.TryGetProperty("metadata", out var metadata)
                        && metadata.TryGetProperty("phone_number_id", out var phoneNumberId))
                        Add(ids, phoneNumberId.ToString());
                }
            }
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }

        return ids.ToList();
    }

    private static void Add(HashSet<string> ids, string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id == "0")
            return;
        ids.Add(id);
    }
}
