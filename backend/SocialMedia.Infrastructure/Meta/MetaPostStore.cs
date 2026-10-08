using System.Text.Json;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Infrastructure.Meta;

/// <summary>
/// Resolves the Post a webhook comment belongs to. A comment is only useful in the Inbox when it
/// carries its post, so a missing post is fetched from Graph and stored (with its media row)
/// instead of being left empty.
/// </summary>
internal static class MetaPostStore
{
    /// <summary>Marks a row saved without Graph data, so it is enriched once and not on every comment.</summary>
    private const string PlaceholderKey = "awaitingGraphFetch";

    /// <summary>
    /// Order: stored post → Graph fetch → placeholder. The placeholder keeps webhook test
    /// deliveries visible, since their sample ids never resolve against Graph.
    /// </summary>
    public static async Task<PostEntityBase> ResolveAsync(
        IProcessDataStore store,
        SocialProfileEntityBase profile,
        Guid platformId,
        string externalPostId,
        DateTime fallbackPublishedAt,
        Func<CancellationToken, Task<RemotePostSnapshot?>> fetchSnapshot,
        string placeholderText,
        bool requireMedia,
        CancellationToken cancellationToken)
    {
        var post = await store.GetPostByExternalIdAsync(profile.Id, externalPostId, cancellationToken)
                   ?? await store.FindPostByExternalIdAsync(externalPostId, cancellationToken);
        if (post is not null)
        {
            if (IsAwaitingGraphFetch(post) || (requireMedia && MetaPostMediaWriter.NeedsGraphRefresh(post)))
                await EnrichAsync(store, post, fetchSnapshot, cancellationToken);
            return post;
        }

        var snapshot = await TryFetchAsync(fetchSnapshot, cancellationToken);
        // Only use the stub label when Graph did not return a post. A real snapshot with
        // no caption (image-only) must stay empty so Inbox can show the media like Facebook.
        var text = snapshot is null
            ? placeholderText
            : (string.IsNullOrWhiteSpace(snapshot.Text) ? string.Empty : snapshot.Text!);

        post = store.NewPost();
        post.SocialProfileId = profile.Id;
        post.PlatformId = platformId;
        post.ExternalPostId = externalPostId;
        post.Status = ContentPostStatus.Published;
        post.PublishedAt = snapshot?.CreatedTime ?? fallbackPublishedAt;
        post.Text = text;
        post.Caption = text;
        post.Type = ResolveType(snapshot);
        post.LikeCount = snapshot?.LikeCount ?? 0;
        post.ShareCount = snapshot?.ShareCount ?? 0;
        post.MetadataJson = BuildMetadata(snapshot);

        await store.AddPostAsync(post, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        await MetaPostMediaWriter.ApplySnapshotAsync(store, post, snapshot, cancellationToken);
        return post;
    }

    /// <summary>Fills in text, media, and counts for a row stored before Graph data was available.</summary>
    private static async Task EnrichAsync(
        IProcessDataStore store,
        PostEntityBase post,
        Func<CancellationToken, Task<RemotePostSnapshot?>> fetchSnapshot,
        CancellationToken cancellationToken)
    {
        var snapshot = await TryFetchAsync(fetchSnapshot, cancellationToken);
        if (snapshot is null)
            return;

        if (!string.IsNullOrWhiteSpace(snapshot.Text))
        {
            post.Text = snapshot.Text;
            post.Caption = snapshot.Text;
        }

        if (snapshot.LikeCount > post.LikeCount) post.LikeCount = snapshot.LikeCount;
        if (snapshot.ShareCount > post.ShareCount) post.ShareCount = snapshot.ShareCount;
        post.PublishedAt ??= snapshot.CreatedTime;
        post.Type = ResolveType(snapshot);
        post.MetadataJson = BuildMetadata(snapshot);

        post.UpdatedAt = DateTime.UtcNow;
        store.UpdatePost(post);
        await store.SaveChangesAsync(cancellationToken);
        await MetaPostMediaWriter.ApplySnapshotAsync(store, post, snapshot, cancellationToken);
    }

    private static async Task<RemotePostSnapshot?> TryFetchAsync(
        Func<CancellationToken, Task<RemotePostSnapshot?>> fetchSnapshot,
        CancellationToken cancellationToken)
    {
        try
        {
            return await fetchSnapshot(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsAwaitingGraphFetch(PostEntityBase post)
    {
        if (MetaPostMediaWriter.IsPlaceholderText(post.Text) || MetaPostMediaWriter.IsPlaceholderText(post.Caption))
            return true;

        if (string.IsNullOrWhiteSpace(post.MetadataJson))
            return string.IsNullOrWhiteSpace(post.Text);

        try
        {
            using var doc = JsonDocument.Parse(post.MetadataJson);
            return doc.RootElement.TryGetProperty(PlaceholderKey, out var flag) && flag.GetBoolean();
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ContentPostType ResolveType(RemotePostSnapshot? snapshot)
    {
        if (snapshot is null) return ContentPostType.Text;
        if (snapshot.IsVideo) return ContentPostType.Video;
        return string.IsNullOrWhiteSpace(snapshot.MediaUrl) ? ContentPostType.Text : ContentPostType.Image;
    }

    private static string BuildMetadata(RemotePostSnapshot? snapshot)
        => snapshot is null
            ? JsonSerializer.Serialize(new Dictionary<string, object> { [PlaceholderKey] = true })
            : JsonSerializer.Serialize(new Dictionary<string, object?> { ["permalink"] = snapshot.Permalink });
}
