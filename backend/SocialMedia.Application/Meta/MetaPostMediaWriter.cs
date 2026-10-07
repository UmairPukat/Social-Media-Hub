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
