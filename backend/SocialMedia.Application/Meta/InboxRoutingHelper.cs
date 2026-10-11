using SocialMedia.Application.DTOs.Inbox;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Application.Meta;

/// <summary>
/// Routes inbox replies to the correct connected account (Integrations vs App Connections).
/// </summary>
public static class InboxRoutingHelper
{
    public static void Apply(
        InboxItemDto item,
        SocialProfileEntityBase profile,
        SocialAccountEntityBase account,
        string menuType)
    {
        item.MenuType = menuType;
        item.PageId = ResolvePageId(profile, account);
        item.AccountId = ResolveAccountId(profile);
    }

    public static string? ResolvePageId(SocialProfileEntityBase profile, SocialAccountEntityBase? account = null)
    {
        if (profile.ProfileType == ProfileType.FacebookPage)
            return profile.ExternalProfileId;

        var fromProfile = ReadMetadataString(profile.MetadataJson, "pageId");
        if (!string.IsNullOrWhiteSpace(fromProfile))
            return fromProfile;

        return account is null ? null : ReadMetadataString(account.MetadataJson, "selectedPageId");
    }

    public static string? ResolveAccountId(SocialProfileEntityBase profile)
    {
        return profile.ProfileType is ProfileType.InstagramLogin or ProfileType.InstagramBusiness
            ? profile.ExternalProfileId
            : null;
    }

    public static bool MatchesConnectedIdentity(
        SocialProfileEntityBase profile,
        SocialAccountEntityBase account,
        string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        if (string.Equals(profile.ExternalProfileId, id, StringComparison.Ordinal))
            return true;
        if (string.Equals(account.ExternalAccountId, id, StringComparison.Ordinal))
            return true;
        if (ProfileMatchesRouting(profile, id, id))
            return true;

        var selectedPageId = ReadMetadataString(account.MetadataJson, "selectedPageId");
        return string.Equals(selectedPageId, id, StringComparison.Ordinal);
    }

    public static bool ProfileMatchesRouting(SocialProfileEntityBase profile, string? pageId, string? accountId)
    {
        if (!string.IsNullOrWhiteSpace(pageId))
        {
            if (string.Equals(profile.ExternalProfileId, pageId, StringComparison.Ordinal))
                return true;

            var metaPageId = ReadMetadataString(profile.MetadataJson, "pageId");
            if (string.Equals(metaPageId, pageId, StringComparison.Ordinal))
                return true;
        }

        if (!string.IsNullOrWhiteSpace(accountId))
        {
            if (string.Equals(profile.ExternalProfileId, accountId, StringComparison.Ordinal))
                return true;

            foreach (var alternateId in ReadAlternateIds(profile.MetadataJson))
            {
                if (string.Equals(alternateId, accountId, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    private static string? ReadMetadataString(string? metadataJson, string property)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(metadataJson);
            return doc.RootElement.TryGetProperty(property, out var value) ? value.GetString() : null;
        }
        catch (System.Text.Json.JsonException)
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
            using var doc = System.Text.Json.JsonDocument.Parse(metadataJson);
            if (!doc.RootElement.TryGetProperty("alternateIds", out var ids) ||
                ids.ValueKind != System.Text.Json.JsonValueKind.Array)
                return Array.Empty<string>();

            return ids.EnumerateArray()
                .Select(id => id.ToString())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList()!;
        }
        catch (System.Text.Json.JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
