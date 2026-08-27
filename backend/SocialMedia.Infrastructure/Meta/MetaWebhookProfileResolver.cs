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
        var profile = await unitOfWork.SocialProfiles.GetByExternalProfileIdAsync(entryId, menuType, cancellationToken);
        if (profile is not null)
            return profile;

        profile = await FindByProfilePageIdAsync(unitOfWork, entryId, menuType, cancellationToken);
        if (profile is not null)
            return await ReloadProfileAsync(unitOfWork, profile, menuType, cancellationToken);

        profile = await FindByAccountSelectedPageIdAsync(unitOfWork, entryId, menuType, cancellationToken);
        if (profile is not null)
            return await ReloadProfileAsync(unitOfWork, profile, menuType, cancellationToken);

        profile = await FindByAlternateIdAsync(unitOfWork, entryId, menuType, cancellationToken);
        if (profile is not null)
            return await ReloadProfileAsync(unitOfWork, profile, menuType, cancellationToken);

        if (WebhookProfileGuard.IsTestDeliveryId(entryId))
        {
            result.Skip($"Test delivery (entry id '{entryId}') ignored — connect a real account to store messages.");
            return null;
        }

        result.Skip($"No connected profile matches entry id '{entryId}'.");
        return null;
    }

    private static async Task<SocialProfile?> ReloadProfileAsync(
        IUnitOfWork unitOfWork,
        SocialProfile snapshot,
        string? menuType,
        CancellationToken cancellationToken)
        => await unitOfWork.SocialProfiles.GetByExternalProfileIdAsync(snapshot.ExternalProfileId, menuType, cancellationToken)
           ?? await unitOfWork.SocialProfiles.GetByIdAsync(snapshot.Id, cancellationToken);

    private static async Task<SocialProfile?> FindByProfilePageIdAsync(
        IUnitOfWork unitOfWork,
        string pageId,
        string? menuType,
        CancellationToken cancellationToken)
    {
        var profiles = await unitOfWork.SocialProfiles.FindAsync(
            p => p.MetadataJson != null
                 && p.MetadataJson.Contains(pageId)
                 && (menuType == null || p.MenuType == menuType),
            cancellationToken);

        return profiles.FirstOrDefault(p => PageIdMatches(p.MetadataJson, pageId));
    }

    private static async Task<SocialProfile?> FindByAccountSelectedPageIdAsync(
        IUnitOfWork unitOfWork,
        string pageId,
        string? menuType,
        CancellationToken cancellationToken)
    {
        var normalizedMenu = string.IsNullOrWhiteSpace(menuType) ? null : MenuTypes.Normalize(menuType);
        var accounts = await unitOfWork.SocialAccounts.FindAsync(
            a => a.Status == SocialAccountStatus.Connected
                 && a.MetadataJson != null
                 && a.MetadataJson.Contains(pageId)
                 && (normalizedMenu == null || a.MenuType == normalizedMenu),
            cancellationToken);

        foreach (var account in accounts.OrderByDescending(a => a.ConnectedAt ?? a.UpdatedAt))
        {
            if (!SelectedPageIdMatches(account.MetadataJson, pageId))
                continue;

            var profiles = await unitOfWork.SocialProfiles.GetBySocialAccountAsync(account.Id, cancellationToken);
            var match = profiles
                .OrderByDescending(p => p.ProfileType == ProfileType.InstagramBusiness ? 1 : 0)
                .ThenByDescending(p => p.ProfileType == ProfileType.FacebookPage ? 1 : 0)
                .ThenByDescending(p => p.ProfileType == ProfileType.InstagramLogin ? 1 : 0)
                .FirstOrDefault();

            if (match is not null)
                return match;
        }

        return null;
    }

    private static async Task<SocialProfile?> FindByAlternateIdAsync(
        IUnitOfWork unitOfWork,
        string externalId,
        string? menuType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            return null;

        var profiles = await unitOfWork.SocialProfiles.FindAsync(
            p => p.MetadataJson != null
                 && p.MetadataJson.Contains(externalId)
                 && (menuType == null || p.MenuType == menuType),
            cancellationToken);

        return profiles.FirstOrDefault(p => ReadAlternateIds(p.MetadataJson).Contains(externalId));
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
