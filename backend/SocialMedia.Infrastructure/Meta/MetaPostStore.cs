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
        var post = await store.GetPostByExternalIdAsync(profile.Id, externalPostId, cancellationToken);
        if (post is not null)
        {
            if (IsAwaitingGraphFetch(post) || (requireMedia && ProcessEntityNav.MediaCount(post) == 0))
                await EnrichAsync(store, post, fetchSnapshot, cancellationToken);
            return post;
        }

        var snapshot = await TryFetchAsync(fetchSnapshot, cancellationToken);
        var text = string.IsNullOrWhiteSpace(snapshot?.Text) ? placeholderText : snapshot!.Text!;

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

        AttachMedia(store, post, snapshot);

        await store.AddPostAsync(post, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
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

        if (ProcessEntityNav.MediaCount(post) == 0)
            AttachMedia(store, post, snapshot);

        post.UpdatedAt = DateTime.UtcNow;
        store.UpdatePost(post);
        await store.SaveChangesAsync(cancellationToken);
    }

    private static void AttachMedia(IProcessDataStore store, PostEntityBase post, RemotePostSnapshot? snapshot)
    {
        var url = FirstNonEmpty(snapshot?.MediaUrl, snapshot?.ThumbnailUrl);
        if (string.IsNullOrWhiteSpace(url))
            return;

        var media = store.NewMedia();
        media.PostId = post.Id;
        media.ExternalMediaId = snapshot!.ExternalId;
        media.MediaType = snapshot.IsVideo ? MediaType.Video : MediaType.Image;
        media.Url = url;
        media.Thumbnail = snapshot.ThumbnailUrl;
        ProcessEntityNav.AttachMedia(post, media);
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

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
