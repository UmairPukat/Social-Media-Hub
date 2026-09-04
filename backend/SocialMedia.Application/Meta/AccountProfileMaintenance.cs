using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Application.Meta;

public static class AccountProfileMaintenance
{
    public static async Task ConsolidateToCanonicalProfileAsync(
        IProcessDataStore store,
        SocialAccountEntityBase account,
        SocialProfileEntityBase canonical,
        CancellationToken cancellationToken)
    {
        var profiles = await store.GetProfilesByAccountAsync(account.Id, cancellationToken);
        foreach (var stale in profiles.Where(p => p.Id != canonical.Id))
        {
            await MovePostsToProfileAsync(store, account, stale.Id, canonical.Id, cancellationToken);
            await MoveConversationsToProfileAsync(store, stale.Id, canonical.Id, cancellationToken);
            store.RemoveSocialProfile(stale);
        }
    }

    public static async Task MovePostsToProfileAsync(
        IProcessDataStore store,
        SocialAccountEntityBase account,
        Guid fromProfileId,
        Guid toProfileId,
        CancellationToken cancellationToken)
    {
        if (fromProfileId == toProfileId)
            return;

        var posts = await store.GetPostsByUserProfilesAsync(account.UserId, account.PlatformId, cancellationToken);
        foreach (var post in posts.Where(p => p.SocialProfileId == fromProfileId))
        {
            post.SocialProfileId = toProfileId;
            post.UpdatedAt = DateTime.UtcNow;
            store.UpdatePost(post);
        }
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
