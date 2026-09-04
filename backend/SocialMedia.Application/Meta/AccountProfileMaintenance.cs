using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Application.Meta;

public static class AccountProfileMaintenance
{
    /// <summary>
    /// Removes profiles that no longer belong to the connected account and deletes their synced posts
    /// so content from a previous account is not relabeled under the new profile name.
    /// </summary>
    public static async Task PurgeStaleProfilesAsync(
        IProcessDataStore store,
        SocialAccountEntityBase account,
        IEnumerable<string> keepExternalProfileIds,
        CancellationToken cancellationToken)
    {
        var keepIds = keepExternalProfileIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        if (keepIds.Count == 0)
            return;

        var profiles = await store.GetProfilesByAccountAsync(account.Id, cancellationToken);
        var canonical = ProcessProfileResolver.PickConnectedProfile(
            profiles,
            keepIds.FirstOrDefault(),
            preferredType: null);
        if (canonical is null)
            return;

        foreach (var profile in profiles)
        {
            if (keepIds.Contains(profile.ExternalProfileId))
                continue;

            await DeletePostsForProfileAsync(store, account, profile.Id, cancellationToken);

            if (profile.Id != canonical.Id)
                await MoveConversationsToProfileAsync(store, profile.Id, canonical.Id, cancellationToken);

            store.RemoveSocialProfileById(profile.Id);
        }
    }

    public static async Task DeletePostsForProfileAsync(
        IProcessDataStore store,
        SocialAccountEntityBase account,
        Guid profileId,
        CancellationToken cancellationToken)
    {
        var posts = await store.GetPostsByUserProfilesAsync(account.UserId, account.PlatformId, cancellationToken);
        foreach (var post in posts.Where(p => p.SocialProfileId == profileId).ToList())
            store.RemovePostById(post.Id);
    }

    public static async Task DeleteAllPostsForAccountAsync(
        IProcessDataStore store,
        SocialAccountEntityBase account,
        CancellationToken cancellationToken)
    {
        var posts = await store.GetPostsByUserProfilesAsync(account.UserId, account.PlatformId, cancellationToken);
        foreach (var post in posts.ToList())
            store.RemovePostById(post.Id);
    }

    public static async Task MoveConversationsToProfileAsync(
        IProcessDataStore store,
        Guid fromProfileId,
        Guid toProfileId,
        CancellationToken cancellationToken)
    {
        if (fromProfileId == toProfileId)
            return;

        var conversations = await store.GetConversationsByProfileIdAsync(fromProfileId, cancellationToken);
        foreach (var conversation in conversations)
        {
            conversation.SocialProfileId = toProfileId;
            conversation.UpdatedAt = DateTime.UtcNow;
            store.UpdateConversation(conversation);
        }
    }
}
