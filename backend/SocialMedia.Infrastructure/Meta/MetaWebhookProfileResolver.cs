using System.Text.Json;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Interfaces;

namespace SocialMedia.Infrastructure.Meta;

/// <summary>
/// Matches Meta webhook <c>entry.id</c> values to a connected profile in the receiving process module.
/// Instagram via Facebook Login sends the linked Page id on object=page while the profile stores the IG id.
/// </summary>
internal static class MetaWebhookProfileResolver
{
    public static async Task<SocialProfile?> ResolveAsync(
        IUnitOfWork unitOfWork,
        string entryId,
        string? menuType,
        WebhookProcessResult result,
        CancellationToken cancellationToken)
    {
        var normalizedMenu = string.IsNullOrWhiteSpace(menuType) ? null : MenuTypes.Normalize(menuType);

        var profile = await unitOfWork.SocialProfiles.GetByExternalProfileIdAsync(entryId, normalizedMenu, cancellationToken);
        if (profile is not null)
            return profile;

        profile = await ScanConnectedProfilesAsync(unitOfWork, entryId, normalizedMenu, cancellationToken);
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

    private static async Task<SocialProfile?> ScanConnectedProfilesAsync(
        IUnitOfWork unitOfWork,
        string entryId,
        string? normalizedMenu,
        CancellationToken cancellationToken)
    {
        var accounts = await unitOfWork.SocialAccounts.FindAsync(
            a => a.Status == SocialAccountStatus.Connected
                 && (normalizedMenu == null || a.MenuType == normalizedMenu),
            cancellationToken);

        foreach (var accountSnapshot in accounts.OrderByDescending(a => a.ConnectedAt ?? a.UpdatedAt))
        {
            if (!SelectedPageIdMatches(accountSnapshot.MetadataJson, entryId))
                continue;

            var profile = await PickProfileForAccountAsync(unitOfWork, accountSnapshot.Id, normalizedMenu, cancellationToken);
            if (profile is not null)
                return profile;
        }

        foreach (var accountSnapshot in accounts)
        {
            var profiles = await unitOfWork.SocialProfiles.GetBySocialAccountAsync(accountSnapshot.Id, cancellationToken);
            foreach (var snapshot in profiles)
            {
                if (PageIdMatches(snapshot.MetadataJson, entryId) ||
                    ReadAlternateIds(snapshot.MetadataJson).Contains(entryId))
                {
                    return await ReloadProfileAsync(unitOfWork, snapshot, normalizedMenu, cancellationToken);
                }
            }
        }

        return null;
    }

    private static async Task<SocialProfile?> PickProfileForAccountAsync(
        IUnitOfWork unitOfWork,
        Guid accountId,
        string? normalizedMenu,
        CancellationToken cancellationToken)
    {
        var profiles = await unitOfWork.SocialProfiles.GetBySocialAccountAsync(accountId, cancellationToken);
        var snapshot = profiles
            .OrderByDescending(p => p.ProfileType == ProfileType.InstagramBusiness ? 1 : 0)
            .ThenByDescending(p => p.ProfileType == ProfileType.InstagramLogin ? 1 : 0)
            .ThenByDescending(p => p.ProfileType == ProfileType.FacebookPage ? 1 : 0)
            .FirstOrDefault();

        return snapshot is null
            ? null
            : await ReloadProfileAsync(unitOfWork, snapshot, normalizedMenu, cancellationToken);
    }

    private static async Task<SocialProfile?> ReloadProfileAsync(
        IUnitOfWork unitOfWork,
        SocialProfile snapshot,
        string? menuType,
        CancellationToken cancellationToken)
        => await unitOfWork.SocialProfiles.GetByExternalProfileIdAsync(snapshot.ExternalProfileId, menuType, cancellationToken)
           ?? await unitOfWork.SocialProfiles.GetByIdAsync(snapshot.Id, cancellationToken);

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
