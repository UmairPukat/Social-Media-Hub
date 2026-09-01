using System.Text.Json;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.YouTube;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.Common.Entities;
using SocialMedia.Application.YouTube;

namespace SocialMedia.Application.Services;

public class YouTubeSyncService : IYouTubeSyncService
{
    private readonly IProcessDataStoreFactory _processData;
    private readonly IYouTubeService _youtube;

    public YouTubeSyncService(IProcessDataStoreFactory processData, IYouTubeService youtube)
    {
        _processData = processData;
        _youtube = youtube;
    }

    public Task<ApiResponse<YouTubeSyncResultDto>> SyncPostsAsync(
        Guid userId,
        string menuType,
        string? platformCode = null,
        CancellationToken cancellationToken = default)
        => SyncInternalAsync(userId, menuType, platformCode, SyncKind.Posts, cancellationToken);

    public Task<ApiResponse<YouTubeSyncResultDto>> SyncCommentsAsync(
        Guid userId,
        string menuType,
        string? platformCode = null,
        CancellationToken cancellationToken = default)
        => SyncInternalAsync(userId, menuType, platformCode, SyncKind.Comments, cancellationToken);

    public Task<ApiResponse<YouTubeSyncResultDto>> SyncStatisticsAsync(
        Guid userId,
        string menuType,
        string? platformCode = null,
        CancellationToken cancellationToken = default)
        => SyncInternalAsync(userId, menuType, platformCode, SyncKind.Statistics, cancellationToken);

    public async Task<ApiResponse<YouTubePostStatisticsDto>> GetPostStatisticsAsync(
        Guid userId,
        Guid postId,
        string menuType,
        bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMenu = MenuTypes.Normalize(menuType);
            var store = _processData.ForMenu(normalizedMenu);
            var post = await store.GetPostByIdAsync(postId, cancellationToken)
                ?? throw new InvalidOperationException("Post not found.");

            var profile = await store.GetProfileByIdAsync(post.SocialProfileId, cancellationToken)
                ?? throw new InvalidOperationException("Profile not found.");
            var account = await store.GetSocialAccountByIdAsync(profile.SocialAccountId, cancellationToken);
            if (account is null || account.UserId != userId)
                throw new InvalidOperationException("Post not found.");

            if (refresh && !string.IsNullOrWhiteSpace(post.ExternalPostId))
            {
                var token = await ResolveAccessTokenAsync(store, account.Id, cancellationToken);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    var snapshots = await _youtube.GetVideoStatisticsAsync(
                        token, [post.ExternalPostId!], cancellationToken);
                    var snapshot = snapshots.FirstOrDefault();
                    if (snapshot is not null)
                    {
                        ApplyStatistics(post, snapshot);
                        post.UpdatedAt = DateTime.UtcNow;
                        store.UpdatePost(post);
                        account.LastSyncAt = DateTime.UtcNow;
                        store.UpdateSocialAccount(account);
                        await store.SaveChangesAsync(cancellationToken);
                    }
                }
            }

            return ApiResponse<YouTubePostStatisticsDto>.Ok(MapStatistics(post));
        }
        catch (Exception ex)
        {
            return ApiResponse<YouTubePostStatisticsDto>.Fail(ex.Message);
        }
    }

    private async Task<ApiResponse<YouTubeSyncResultDto>> SyncInternalAsync(
        Guid userId,
        string menuType,
        string? platformCode,
        SyncKind kind,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalizedMenu = MenuTypes.Normalize(menuType);
            var code = string.IsNullOrWhiteSpace(platformCode) ? "youtube" : platformCode.Trim().ToLowerInvariant();
            var store = _processData.ForMenu(normalizedMenu);
            var context = await ResolveYouTubeContextAsync(store, userId, code, cancellationToken);
            if (context is null)
                return ApiResponse<YouTubeSyncResultDto>.Fail("Connect YouTube and save Google OAuth credentials before syncing.");

            var (account, profile, token) = context.Value;
            var result = new YouTubeSyncResultDto
            {
                MenuType = normalizedMenu,
                PlatformCode = code
            };

            switch (kind)
            {
                case SyncKind.Posts:
                    await SyncPostsForProfileAsync(store, account, profile, token, result, cancellationToken);
                    result.Message = $"Fetched {result.Fetched} videos; stored {result.Stored}, updated {result.Updated}.";
                    break;
                case SyncKind.Comments:
                    await SyncCommentsForProfileAsync(store, account, profile, token, result, cancellationToken);
                    if (result.Skipped > 0 && result.Fetched == 0 && result.Stored == 0 && result.Updated == 0)
                    {
                        result.Message =
                            $"No comments were fetched. {result.Skipped} video(s) have comments disabled on YouTube. Enable comments in YouTube Studio or fetch posts again after enabling comments.";
                    }
                    else if (result.Skipped > 0)
                    {
                        result.Message =
                            $"Fetched {result.Fetched} comments; stored {result.Stored}, updated {result.Updated}; skipped {result.Skipped} video(s) with comments disabled on YouTube.";
                    }
                    else
                    {
                        result.Message = $"Fetched {result.Fetched} comments; stored {result.Stored}, updated {result.Updated}.";
                    }
                    break;
                case SyncKind.Statistics:
                    await SyncStatisticsForProfileAsync(store, account, profile, token, result, cancellationToken);
                    result.Message = $"Refreshed statistics for {result.Updated} videos.";
                    break;
            }

            account.LastSyncAt = DateTime.UtcNow;
            store.UpdateSocialAccount(account);
            await store.SaveChangesAsync(cancellationToken);
            return ApiResponse<YouTubeSyncResultDto>.Ok(result, result.Message ?? "Success");
        }
        catch (Exception ex)
        {
            return ApiResponse<YouTubeSyncResultDto>.Fail(ex.Message);
        }
    }

    private async Task SyncPostsForProfileAsync(
        IProcessDataStore store,
        SocialAccountEntityBase account,
        SocialProfileEntityBase profile,
        string accessToken,
        YouTubeSyncResultDto result,
        CancellationToken cancellationToken)
    {
        var videos = await _youtube.ListChannelVideosAsync(
            accessToken, profile.ExternalProfileId, maxResults: 50, cancellationToken);
        result.Fetched = videos.Count;

        foreach (var video in videos)
        {
            var existing = await store.GetPostByExternalIdAsync(profile.Id, video.VideoId, cancellationToken);
            if (existing is null)
            {
                var post = store.NewPost();
                post.SocialProfileId = profile.Id;
                post.PlatformId = account.PlatformId;
                post.ExternalPostId = video.VideoId;
                post.Text = video.Title;
                post.Caption = video.Description ?? video.Title;
                post.Type = ContentPostType.Video;
                post.Status = ContentPostStatus.Published;
                post.PublishedAt = video.PublishedAt ?? DateTime.UtcNow;
                ApplyStatistics(post, video);
                post.MetadataJson = BuildPostMetadata(video);
                await store.AddPostAsync(post, cancellationToken);
                AttachThumbnail(store, post, video);
                result.Stored++;
                continue;
            }

            existing.Text = video.Title;
            existing.Caption = video.Description ?? video.Title;
            existing.PublishedAt ??= video.PublishedAt;
            ApplyStatistics(existing, video);
            existing.MetadataJson = BuildPostMetadata(video);
            existing.UpdatedAt = DateTime.UtcNow;
            store.UpdatePost(existing);
            AttachThumbnail(store, existing, video);
            result.Updated++;
        }
    }

    private async Task SyncCommentsForProfileAsync(
        IProcessDataStore store,
        SocialAccountEntityBase account,
        SocialProfileEntityBase profile,
        string accessToken,
        YouTubeSyncResultDto result,
        CancellationToken cancellationToken)
    {
        var posts = await store.GetPostsByUserProfilesAsync(account.UserId, account.PlatformId, cancellationToken);
        var profilePosts = posts
            .Where(p => p.SocialProfileId == profile.Id && !string.IsNullOrWhiteSpace(p.ExternalPostId))
            .ToList();

        foreach (var post in profilePosts)
        {
            IReadOnlyList<YouTubeCommentSnapshot> comments;
            try
            {
                comments = await _youtube.ListVideoCommentsAsync(
                    accessToken, post.ExternalPostId!, maxResults: 50, cancellationToken);
            }
            catch (Exception ex) when (IsCommentsDisabledError(ex))
            {
                result.Skipped++;
                continue;
            }

            result.Fetched += comments.Count;

            var localByExternal = new Dictionary<string, Guid>(StringComparer.Ordinal);
            foreach (var remote in comments.OrderBy(c => c.ParentCommentId is null ? 0 : 1))
            {
                var existing = await store.GetCommentByExternalIdAsync(remote.CommentId, cancellationToken);
                if (existing is not null)
                {
                    existing.AuthorName = remote.AuthorName;
                    existing.AuthorId = remote.AuthorChannelId;
                    existing.Message = remote.Message;
                    existing.LikeCount = (int)Math.Min(remote.LikeCount, int.MaxValue);
                    existing.PlatformCreatedAt = remote.PublishedAt ?? existing.PlatformCreatedAt;
                    if (remote.ParentCommentId is not null &&
                        localByExternal.TryGetValue(remote.ParentCommentId, out var parentId))
                        existing.ParentCommentId = parentId;
                    store.UpdateComment(existing);
                    localByExternal[remote.CommentId] = existing.Id;
                    result.Updated++;
                    continue;
                }

                var comment = store.NewComment();
                comment.PostId = post.Id;
                comment.ExternalCommentId = remote.CommentId;
                comment.AuthorName = remote.AuthorName;
                comment.AuthorId = remote.AuthorChannelId;
                comment.Message = remote.Message;
                comment.LikeCount = (int)Math.Min(remote.LikeCount, int.MaxValue);
                comment.PlatformCreatedAt = remote.PublishedAt ?? DateTime.UtcNow;
                if (remote.ParentCommentId is not null &&
                    localByExternal.TryGetValue(remote.ParentCommentId, out var parentLocalId))
                    comment.ParentCommentId = parentLocalId;

                await store.AddCommentAsync(comment, cancellationToken);
                await store.SaveChangesAsync(cancellationToken);
                localByExternal[remote.CommentId] = comment.Id;
                result.Stored++;
            }

            post.CommentCount = Math.Max(post.CommentCount, comments.Count(c => c.ParentCommentId is null));
            post.UpdatedAt = DateTime.UtcNow;
            store.UpdatePost(post);
        }
    }

    private async Task SyncStatisticsForProfileAsync(
        IProcessDataStore store,
        SocialAccountEntityBase account,
        SocialProfileEntityBase profile,
        string accessToken,
        YouTubeSyncResultDto result,
        CancellationToken cancellationToken)
    {
        var posts = await store.GetPostsByUserProfilesAsync(account.UserId, account.PlatformId, cancellationToken);
        var profilePosts = posts
            .Where(p => p.SocialProfileId == profile.Id && !string.IsNullOrWhiteSpace(p.ExternalPostId))
            .ToList();

        foreach (var batch in profilePosts.Chunk(50))
        {
            var ids = batch.Select(p => p.ExternalPostId!).ToList();
            var snapshots = await _youtube.GetVideoStatisticsAsync(accessToken, ids, cancellationToken);
            var byId = snapshots.ToDictionary(v => v.VideoId, StringComparer.Ordinal);
            result.Fetched += ids.Count;

            foreach (var post in batch)
            {
                if (!byId.TryGetValue(post.ExternalPostId!, out var snapshot))
                {
                    result.Skipped++;
                    continue;
                }

                ApplyStatistics(post, snapshot);
                post.Text = snapshot.Title;
                post.Caption = snapshot.Description ?? snapshot.Title;
                post.UpdatedAt = DateTime.UtcNow;
                store.UpdatePost(post);
                result.Updated++;
            }
        }
    }

    private static void ApplyStatistics(PostEntityBase post, YouTubeVideoSnapshot snapshot)
    {
        post.ViewCount = (int)Math.Min(snapshot.ViewCount, int.MaxValue);
        post.LikeCount = (int)Math.Min(snapshot.LikeCount, int.MaxValue);
        post.CommentCount = (int)Math.Min(snapshot.CommentCount, int.MaxValue);
    }

    private static void AttachThumbnail(IProcessDataStore store, PostEntityBase post, YouTubeVideoSnapshot video)
    {
        if (string.IsNullOrWhiteSpace(video.ThumbnailUrl) || ProcessEntityNav.MediaCount(post) > 0)
            return;

        var media = store.NewMedia();
        media.PostId = post.Id;
        media.ExternalMediaId = video.VideoId;
        media.MediaType = MediaType.Image;
        media.Url = video.ThumbnailUrl;
        media.Thumbnail = video.ThumbnailUrl;
        ProcessEntityNav.AttachMedia(post, media);
    }

    private static string BuildPostMetadata(YouTubeVideoSnapshot video)
        => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["permalink"] = video.Permalink,
            ["source"] = "youtube"
        });

    private static YouTubePostStatisticsDto MapStatistics(PostEntityBase post)
    {
        string? permalink = null;
        string? thumbnail = null;
        if (!string.IsNullOrWhiteSpace(post.MetadataJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(post.MetadataJson);
                permalink = doc.RootElement.TryGetProperty("permalink", out var link) ? link.GetString() : null;
            }
            catch (JsonException)
            {
                // ignore malformed metadata
            }
        }

        return new YouTubePostStatisticsDto
        {
            PostId = post.Id,
            ExternalPostId = post.ExternalPostId,
            Title = post.Text ?? post.Caption ?? "YouTube video",
            Description = post.Caption,
            ThumbnailUrl = thumbnail,
            Permalink = permalink,
            ViewCount = post.ViewCount,
            LikeCount = post.LikeCount,
            CommentCount = post.CommentCount,
            ShareCount = post.ShareCount,
            PublishedAt = post.PublishedAt,
            RefreshedAt = post.UpdatedAt ?? post.CreatedAt
        };
    }

    private async Task<(SocialAccountEntityBase Account, SocialProfileEntityBase Profile, string Token)?> ResolveYouTubeContextAsync(
        IProcessDataStore store,
        Guid userId,
        string platformCode,
        CancellationToken cancellationToken)
    {
        var platform = await store.GetPlatformByCodeAsync(platformCode, cancellationToken);
        if (platform is null)
            return null;

        var account = await store.GetSocialAccountByUserAndPlatformAsync(userId, platform.Id, cancellationToken);
        if (account is null || account.Status != SocialAccountStatus.Connected)
            return null;

        var token = await ResolveAccessTokenAsync(store, account.Id, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var profiles = await store.GetProfilesByAccountAsync(account.Id, cancellationToken);
        var profile = profiles.FirstOrDefault(p => p.ProfileType == ProfileType.YouTubeChannel)
            ?? profiles.FirstOrDefault();
        if (profile is null || string.IsNullOrWhiteSpace(profile.ExternalProfileId))
            return null;

        return (account, profile, token);
    }

    private static async Task<string?> ResolveAccessTokenAsync(
        IProcessDataStore store,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var auth = await store.GetSocialAuthByAccountIdAsync(accountId, cancellationToken);
        if (auth is null)
            return null;

        if (!string.IsNullOrWhiteSpace(auth.AccessToken))
            return auth.AccessToken;

        return null;
    }

    private static bool IsCommentsDisabledError(Exception ex)
        => ex is YouTubeCommentsDisabledException || YouTubeApiErrors.IsCommentsDisabledMessage(ex.Message);

    private enum SyncKind
    {
        Posts,
        Comments,
        Statistics
    }
}
