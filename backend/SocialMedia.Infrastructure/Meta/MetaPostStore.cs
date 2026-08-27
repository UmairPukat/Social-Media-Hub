using System.Text.Json;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Interfaces;

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
    public static async Task<Post> ResolveAsync(
        IUnitOfWork unitOfWork,
        SocialProfile profile,
        Guid platformId,
        string externalPostId,
        DateTime fallbackPublishedAt,
        string menuType,
        Func<CancellationToken, Task<RemotePostSnapshot?>> fetchSnapshot,
        string placeholderText,
        bool requireMedia,
        CancellationToken cancellationToken)
    {
        var post = await unitOfWork.Posts.GetByExternalPostIdAsync(profile.Id, externalPostId, menuType, cancellationToken);
        if (post is not null)
        {
            // Instagram posts always have media. A previously stored row may predate media
            // enrichment, so fill it before attaching the incoming comment.
            if (IsAwaitingGraphFetch(post) || (requireMedia && post.MediaItems.Count == 0))
                await EnrichAsync(unitOfWork, post, fetchSnapshot, cancellationToken);
            return post;
        }

        var snapshot = await TryFetchAsync(fetchSnapshot, cancellationToken);
        var text = string.IsNullOrWhiteSpace(snapshot?.Text) ? placeholderText : snapshot!.Text!;

        post = new Post
        {
            SocialProfileId = profile.Id,
            PlatformId = platformId,
            ExternalPostId = externalPostId,
            MenuType = menuType,
            Status = ContentPostStatus.Published,
            PublishedAt = snapshot?.CreatedTime ?? fallbackPublishedAt,
            Text = text,
            Caption = text,
            Type = ResolveType(snapshot),
            LikeCount = snapshot?.LikeCount ?? 0,
            ShareCount = snapshot?.ShareCount ?? 0,
            MetadataJson = BuildMetadata(snapshot)
        };

        AttachMedia(post, snapshot);

        await unitOfWork.Posts.AddAsync(post, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return post;
    }

    /// <summary>Fills in text, media, and counts for a row stored before Graph data was available.</summary>
    private static async Task EnrichAsync(
        IUnitOfWork unitOfWork,
        Post post,
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

        if (post.MediaItems.Count == 0)
            AttachMedia(post, snapshot);

        post.UpdatedAt = DateTime.UtcNow;
        unitOfWork.Posts.Update(post);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Added through the navigation property so EF writes the Media row with the post.</summary>
    private static void AttachMedia(Post post, RemotePostSnapshot? snapshot)
    {
        var url = FirstNonEmpty(snapshot?.MediaUrl, snapshot?.ThumbnailUrl);
        if (string.IsNullOrWhiteSpace(url))
            return;

        post.MediaItems.Add(new Media
        {
            PostId = post.Id,
            ExternalMediaId = snapshot!.ExternalId,
            MediaType = snapshot.IsVideo ? MediaType.Video : MediaType.Image,
            Url = url,
            Thumbnail = snapshot.ThumbnailUrl
        });
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
            // Sample ids from Meta's test tool and revoked tokens both fail here. The comment is
            // still stored, so the caller continues with a placeholder post.
            return null;
        }
    }

    private static bool IsAwaitingGraphFetch(Post post)
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
