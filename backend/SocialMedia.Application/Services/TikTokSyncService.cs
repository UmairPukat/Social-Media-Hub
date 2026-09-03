using System.Text.Json;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.TikTok;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Application.Services;

public class TikTokSyncService : ITikTokSyncService
{
    private readonly IProcessDataStoreFactory _processData;
    private readonly ITikTokService _tikTok;

    public TikTokSyncService(IProcessDataStoreFactory processData, ITikTokService tikTok)
    {
        _processData = processData;
        _tikTok = tikTok;
    }

    public Task<ApiResponse<TikTokSyncResultDto>> SyncPostsAsync(
        Guid userId,
        string menuType,
        string? platformCode = null,
        CancellationToken cancellationToken = default)
        => SyncInternalAsync(userId, menuType, platformCode, SyncKind.Posts, cancellationToken);

    public Task<ApiResponse<TikTokSyncResultDto>> SyncStatisticsAsync(
        Guid userId,
        string menuType,
        string? platformCode = null,
        CancellationToken cancellationToken = default)
        => SyncInternalAsync(userId, menuType, platformCode, SyncKind.Statistics, cancellationToken);

    private async Task<ApiResponse<TikTokSyncResultDto>> SyncInternalAsync(
        Guid userId,
        string menuType,
        string? platformCode,
        SyncKind kind,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalizedMenu = MenuTypes.Normalize(menuType);
            var code = string.IsNullOrWhiteSpace(platformCode) ? "tiktok" : platformCode.Trim().ToLowerInvariant();
            var store = _processData.ForMenu(normalizedMenu);
            var context = await ResolveTikTokContextAsync(store, userId, code, cancellationToken);
            if (context is null)
                return ApiResponse<TikTokSyncResultDto>.Fail("Connect TikTok and save OAuth credentials before syncing.");

            var (account, profile, token) = context.Value;
            var result = new TikTokSyncResultDto
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
                case SyncKind.Statistics:
                    await SyncStatisticsForProfileAsync(store, account, profile, token, result, cancellationToken);
                    result.Message = $"Refreshed statistics for {result.Updated} videos.";
                    break;
            }

            account.LastSyncAt = DateTime.UtcNow;
            store.UpdateSocialAccount(account);
            await store.SaveChangesAsync(cancellationToken);
            return ApiResponse<TikTokSyncResultDto>.Ok(result, result.Message ?? "Success");
        }
        catch (Exception ex)
        {
            return ApiResponse<TikTokSyncResultDto>.Fail(ex.Message);
        }
    }

    private async Task SyncPostsForProfileAsync(
        IProcessDataStore store,
        SocialAccountEntityBase account,
        SocialProfileEntityBase profile,
        string accessToken,
        TikTokSyncResultDto result,
        CancellationToken cancellationToken)
    {
        var videos = await _tikTok.ListVideosAsync(accessToken, maxResults: 50, cancellationToken);
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
                post.PublishedAt = video.CreateTime ?? DateTime.UtcNow;
                ApplyStatistics(post, video);
                post.MetadataJson = BuildPostMetadata(video);
                await store.AddPostAsync(post, cancellationToken);
                AttachThumbnail(store, post, video);
                result.Stored++;
                continue;
            }

            existing.Text = video.Title;
            existing.Caption = video.Description ?? video.Title;
            existing.PublishedAt ??= video.CreateTime;
            ApplyStatistics(existing, video);
            existing.MetadataJson = BuildPostMetadata(video);
            existing.UpdatedAt = DateTime.UtcNow;
            store.UpdatePost(existing);
            AttachThumbnail(store, existing, video);
            result.Updated++;
        }
    }

    private async Task SyncStatisticsForProfileAsync(
        IProcessDataStore store,
        SocialAccountEntityBase account,
        SocialProfileEntityBase profile,
        string accessToken,
        TikTokSyncResultDto result,
        CancellationToken cancellationToken)
    {
        var posts = await store.GetPostsByUserProfilesAsync(account.UserId, account.PlatformId, cancellationToken);
        var profilePosts = posts
            .Where(p => p.SocialProfileId == profile.Id && !string.IsNullOrWhiteSpace(p.ExternalPostId))
            .ToList();

        foreach (var batch in profilePosts.Chunk(20))
        {
            var ids = batch.Select(p => p.ExternalPostId!).ToList();
            var snapshots = await _tikTok.QueryVideosAsync(accessToken, ids, cancellationToken);
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
                post.MetadataJson = BuildPostMetadata(snapshot);
                post.UpdatedAt = DateTime.UtcNow;
                store.UpdatePost(post);
                result.Updated++;
            }
        }
    }

    private static void ApplyStatistics(PostEntityBase post, TikTokVideoSnapshot snapshot)
    {
        post.ViewCount = (int)Math.Min(snapshot.ViewCount, int.MaxValue);
        post.LikeCount = (int)Math.Min(snapshot.LikeCount, int.MaxValue);
        post.CommentCount = (int)Math.Min(snapshot.CommentCount, int.MaxValue);
        post.ShareCount = (int)Math.Min(snapshot.ShareCount, int.MaxValue);
    }

    private static void AttachThumbnail(IProcessDataStore store, PostEntityBase post, TikTokVideoSnapshot video)
    {
        if (string.IsNullOrWhiteSpace(video.CoverImageUrl) || ProcessEntityNav.MediaCount(post) > 0)
            return;

        var media = store.NewMedia();
        media.PostId = post.Id;
        media.ExternalMediaId = video.VideoId;
        media.MediaType = MediaType.Image;
        media.Url = video.CoverImageUrl;
        media.Thumbnail = video.CoverImageUrl;
        ProcessEntityNav.AttachMedia(post, media);
    }

    private static string BuildPostMetadata(TikTokVideoSnapshot video)
        => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["permalink"] = video.ShareUrl,
            ["source"] = "tiktok"
        });

    private async Task<(SocialAccountEntityBase Account, SocialProfileEntityBase Profile, string Token)?> ResolveTikTokContextAsync(
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
        var profile = profiles.FirstOrDefault(p => p.ProfileType == ProfileType.TikTokAccount)
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

    private enum SyncKind
    {
        Posts,
        Statistics
    }
}
