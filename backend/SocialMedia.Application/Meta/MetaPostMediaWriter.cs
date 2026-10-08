using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Application.Meta;

/// <summary>
/// Writes Graph media as its own row. Updating an AsNoTracking post strips
/// navigation collections, so media must be inserted independently of the post.
/// </summary>
public static class MetaPostMediaWriter
{
    public static bool IsPlaceholderText(string? text)
        => string.IsNullOrWhiteSpace(text)
           || string.Equals(text, "Instagram post", StringComparison.OrdinalIgnoreCase)
           || string.Equals(text, "Facebook post", StringComparison.OrdinalIgnoreCase)
           || text.StartsWith("Instagram post ", StringComparison.OrdinalIgnoreCase)
           || text.StartsWith("Facebook post ", StringComparison.OrdinalIgnoreCase);

    public static bool NeedsGraphRefresh(PostEntityBase post)
        => IsPlaceholderText(post.Text)
           || IsPlaceholderText(post.Caption)
           || !ProcessEntityNav.HasDisplayableMedia(post);

    public static async Task ApplySnapshotAsync(
        IProcessDataStore store,
        PostEntityBase post,
        RemotePostSnapshot? snapshot,
        CancellationToken cancellationToken = default)
    {
        if (snapshot is null)
            return;

        var changed = false;
        if (!string.IsNullOrWhiteSpace(snapshot.Text) &&
            (IsPlaceholderText(post.Text) || IsPlaceholderText(post.Caption)))
        {
            post.Text = snapshot.Text;
            post.Caption = snapshot.Text;
            changed = true;
        }

        if (snapshot.LikeCount > post.LikeCount)
        {
            post.LikeCount = snapshot.LikeCount;
            changed = true;
        }

        if (snapshot.ShareCount > post.ShareCount)
        {
            post.ShareCount = snapshot.ShareCount;
            changed = true;
        }

        if (snapshot.CommentCount > post.CommentCount)
        {
            post.CommentCount = snapshot.CommentCount;
            changed = true;
        }

        if (snapshot.CreatedTime.HasValue && post.PublishedAt is null)
        {
            post.PublishedAt = snapshot.CreatedTime;
            changed = true;
        }

        if (snapshot.IsVideo)
            post.Type = ContentPostType.Video;
        else if (!string.IsNullOrWhiteSpace(snapshot.MediaUrl) || !string.IsNullOrWhiteSpace(snapshot.ThumbnailUrl))
            post.Type = ContentPostType.Image;

        if (changed)
        {
            post.UpdatedAt = DateTime.UtcNow;
            store.UpdatePost(post);
            await store.SaveChangesAsync(cancellationToken);
        }

        await PersistAsync(store, post, snapshot, cancellationToken);
    }

    public static async Task<bool> PersistAsync(
        IProcessDataStore store,
        PostEntityBase post,
        RemotePostSnapshot? snapshot,
        CancellationToken cancellationToken = default)
    {
        if (snapshot is null || ProcessEntityNav.HasDisplayableMedia(post))
            return false;

        var url = FirstNonEmpty(snapshot.MediaUrl, snapshot.ThumbnailUrl);
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var media = store.NewMedia();
        media.PostId = post.Id;
        media.ExternalMediaId = snapshot.ExternalId;
        media.MediaType = snapshot.IsVideo ? MediaType.Video : MediaType.Image;
        media.Url = url;
        media.Thumbnail = snapshot.ThumbnailUrl;
        await store.AddMediaAsync(media, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        ProcessEntityNav.AttachMedia(post, media);
        return true;
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
