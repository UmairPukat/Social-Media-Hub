using System.Text.Json;
using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Infrastructure.Meta;

/// <summary>
/// Matches Meta webhook <c>entry.id</c> values to a connected profile in the receiving process module.
/// Instagram via Facebook Login sends the linked Page id on object=page while the profile stores the IG id.
/// </summary>
internal static class MetaWebhookProfileResolver
{
    public static async Task<SocialProfileEntityBase?> ResolveAsync(
        IProcessDataStore store,
        string entryId,
        WebhookProcessResult result,
        CancellationToken cancellationToken)
    {
        var profile = await store.GetProfileByExternalIdAsync(entryId, cancellationToken);
        if (profile is not null)
            return profile;

        profile = await ScanConnectedProfilesAsync(store, entryId, cancellationToken);
        if (profile is not null)
            return profile;

        if (WebhookProfileGuard.IsTestDeliveryId(entryId))
        {
            result.Skip($"Test delivery (entry id '{entryId}') ignored — connect a real account to store messages.");
            return null;
        }

        result.Skip(
            $"No connected profile matches entry id '{entryId}'. Reconnect under this module and select the correct page/Instagram account.");
        return null;
    }

    /// <summary>Profile lookup without adding skip notes (used while probing alternate ids).</summary>
    public static async Task<SocialProfileEntityBase?> TryResolveAsync(
        IProcessDataStore store,
        string entryId,
        CancellationToken cancellationToken)
    {
        var profile = await store.GetProfileByExternalIdAsync(entryId, cancellationToken);
        if (profile is not null)
            return profile;

        return await ScanConnectedProfilesAsync(store, entryId, cancellationToken);
    }

    private static async Task<SocialProfileEntityBase?> ScanConnectedProfilesAsync(
        IProcessDataStore store,
        string entryId,
        CancellationToken cancellationToken)
    {
        var accounts = await store.FindConnectedSocialAccountsAsync(cancellationToken);

        foreach (var accountSnapshot in accounts.OrderByDescending(a => a.ConnectedAt ?? a.UpdatedAt))
        {
            if (!SelectedPageIdMatches(accountSnapshot.MetadataJson, entryId))
                continue;

            var profile = await store.PickBestProfileForAccountAsync(accountSnapshot.Id, cancellationToken);
            if (profile is not null)
                return profile;
        }

        foreach (var accountSnapshot in accounts)
        {
            var profiles = await store.GetProfilesByAccountAsync(accountSnapshot.Id, cancellationToken);
            foreach (var snapshot in profiles)
            {
                if (PageIdMatches(snapshot.MetadataJson, entryId) ||
                    ReadAlternateIds(snapshot.MetadataJson).Contains(entryId))
                {
                    return await store.GetProfileByExternalIdAsync(snapshot.ExternalProfileId, cancellationToken)
                           ?? await store.GetProfileByIdAsync(snapshot.Id, cancellationToken);
                }
            }
        }

        return null;
    }

    private static bool PageIdMatches(string? metadataJson, string pageId)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return false;

        try
        {
            using var meta = JsonDocument.Parse(metadataJson);
            return meta.RootElement.TryGetProperty("pageId", out var id)
                   && string.Equals(id.GetString(), pageId, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool SelectedPageIdMatches(string? metadataJson, string pageId)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return false;

        try
        {
            using var meta = JsonDocument.Parse(metadataJson);
            return meta.RootElement.TryGetProperty("selectedPageId", out var id)
                   && string.Equals(id.GetString(), pageId, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
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
