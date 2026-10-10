using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Application.Meta;

/// <summary>
/// Decides left vs right for DMs from the connected card (the account you logged in),
/// never from the other person in the thread.
/// </summary>
public static class MetaConversationSide
{
    public static bool OwnsActor(
        SocialAccountEntityBase? account,
        IEnumerable<SocialProfileEntityBase>? profiles,
        string? actorId,
        params string?[] extraIds)
        => OwnsActor(account is null ? null : [account], profiles, actorId, extraIds);

    public static bool OwnsActor(
        IEnumerable<SocialAccountEntityBase>? accounts,
        IEnumerable<SocialProfileEntityBase>? profiles,
        string? actorId,
        params string?[] extraIds)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return false;

        if (accounts is not null)
        {
            foreach (var account in accounts)
            {
                if (string.Equals(account.ExternalAccountId, actorId, StringComparison.Ordinal))
                    return true;
                if (IdEquals(ReadJsonString(account.MetadataJson, "selectedPageId"), actorId))
                    return true;
            }
        }

        foreach (var extra in extraIds)
        {
            if (IdEquals(extra, actorId))
                return true;
        }

        if (profiles is null)
            return false;

        foreach (var profile in profiles)
        {
            if (OwnsProfileId(profile, actorId))
                return true;
        }

        return false;
    }

    public static bool IsOutgoing(
        SocialAccountEntityBase? account,
        IEnumerable<SocialProfileEntityBase>? profiles,
        string? senderId,
        string? receiverId,
        bool storedOutbound,
        params string?[] extraIds)
        => IsOutgoing(account is null ? null : [account], profiles, senderId, receiverId, storedOutbound, extraIds);

    public static bool IsOutgoing(
        IEnumerable<SocialAccountEntityBase>? accounts,
        IEnumerable<SocialProfileEntityBase>? profiles,
        string? senderId,
        string? receiverId,
        bool storedOutbound,
        params string?[] extraIds)
    {
        var senderIsUs = OwnsActor(accounts, profiles, senderId, extraIds);
        var receiverIsUs = OwnsActor(accounts, profiles, receiverId, extraIds);

        if (senderIsUs && !receiverIsUs)
            return true;
        if (receiverIsUs && !senderIsUs)
            return false;

        return storedOutbound;
    }

    public static string? ResolveCustomerId(
        SocialAccountEntityBase? account,
        IEnumerable<SocialProfileEntityBase>? profiles,
        string? senderId,
        string? receiverId,
        bool outbound,
        params string?[] extraIds)
    {
        var senderIsUs = OwnsActor(account, profiles, senderId, extraIds);
        var receiverIsUs = OwnsActor(account, profiles, receiverId, extraIds);

        if (outbound || senderIsUs)
            return FirstNonOwned(account, profiles, extraIds, receiverId, senderId);

        if (receiverIsUs)
            return FirstNonOwned(account, profiles, extraIds, senderId, receiverId);

        return FirstNonOwned(account, profiles, extraIds, senderId, receiverId);
    }

    public static string? ResolvePeerId(
        SocialAccountEntityBase? account,
        IEnumerable<SocialProfileEntityBase>? profiles,
        string? customerId,
        string? senderId,
        string? receiverId)
    {
        if (!string.IsNullOrWhiteSpace(customerId) && !OwnsActor(account, profiles, customerId))
            return customerId;

        return FirstNonOwned(account, profiles, [], senderId, receiverId, customerId);
    }

    public static bool OwnsProfileId(SocialProfileEntityBase profile, string? actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return false;

        if (string.Equals(profile.ExternalProfileId, actorId, StringComparison.Ordinal))
            return true;
        if (IdEquals(ReadJsonString(profile.MetadataJson, "pageId"), actorId))
            return true;

        foreach (var alternateId in ReadAlternateIds(profile.MetadataJson))
        {
            if (string.Equals(alternateId, actorId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string? FirstNonOwned(
        SocialAccountEntityBase? account,
        IEnumerable<SocialProfileEntityBase>? profiles,
        string?[] extraIds,
        params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && !OwnsActor(account, profiles, candidate, extraIds))
                return candidate;
        }

        return candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static bool IdEquals(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left)
           && string.Equals(left, right, StringComparison.Ordinal);

    private static string? ReadJsonString(string? json, string property)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
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
                .ToList();
        }
        catch (System.Text.Json.JsonException)
        {
            return Array.Empty<string>();
        }
    }
}