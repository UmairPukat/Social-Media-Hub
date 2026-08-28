using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Posts;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Application.Services;

public class PostService : IPostService
{
    private readonly IProcessDataStoreFactory _processData;
    private readonly IFacebookService _facebookService;
    private readonly IInstagramService _instagramService;

    public PostService(
        IProcessDataStoreFactory processData,
        IFacebookService facebookService,
        IInstagramService instagramService)
    {
        _processData = processData;
        _facebookService = facebookService;
        _instagramService = instagramService;
    }

    public async Task<ApiResponse<PublishPostResponse>> CreateAndPublishAsync(
        Guid userId,
        CreatePostRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var located = await ProcessStoreLocator.FindAsync(
                _processData,
                store => store.GetProfileByIdAsync(request.SocialProfileId, cancellationToken),
                cancellationToken);
            if (located is null)
                return ApiResponse<PublishPostResponse>.Fail("Social profile not found.");

            var (store, profile) = located.Value;
            var account = await store.GetSocialAccountWithAuthAndProfilesAsync(profile.SocialAccountId, cancellationToken);
            if (account is null || account.UserId != userId || ProcessEntityNav.Auth(account) is null)
                return ApiResponse<PublishPostResponse>.Fail("Connected account not found.");

            var platform = await store.GetPlatformByIdAsync(account.PlatformId, cancellationToken);
            var post = store.NewPost();
            post.SocialProfileId = profile.Id;
            post.PlatformId = account.PlatformId;
            post.Text = request.Content;
            post.Caption = request.Content;
            post.Type = string.IsNullOrWhiteSpace(request.MediaUrl) ? ContentPostType.Text : ContentPostType.Image;
            post.Status = ContentPostStatus.Draft;
            await store.AddPostAsync(post, cancellationToken);
            await store.SaveChangesAsync(cancellationToken);

            var auth = ProcessEntityNav.Auth(account)!;
            var context = new MetaCallContext
            {
                AccessToken = auth.AccessToken,
                ProfileExternalId = profile.ExternalProfileId
            };

            try
            {
                var code = platform?.Code?.ToLowerInvariant();
                if (code == "facebook")
                {
                    var result = await _facebookService.CreatePostAsync(context, request.Content, request.MediaUrl, cancellationToken);
                    post.ExternalPostId = result.Id;
                    post.Status = ContentPostStatus.Published;
                    post.PublishedAt = DateTime.UtcNow;
                }
                else if (code == "instagram")
                {
                    var result = await _instagramService.CreatePostAsync(context, request.Content, request.MediaUrl, cancellationToken);
                    post.ExternalPostId = result.Id;
                    post.Status = ContentPostStatus.Published;
                    post.PublishedAt = DateTime.UtcNow;
                }
                else
                {
                    post.Status = ContentPostStatus.Failed;
                    post.ErrorMessage = $"Publishing not supported for {code}.";
                }
            }
            catch (Exception ex)
            {
                post.Status = ContentPostStatus.Failed;
                post.ErrorMessage = ex.Message;
            }

            store.UpdatePost(post);
            await store.SaveChangesAsync(cancellationToken);

            var dto = Map(post, platform?.Code, profile);
            return ApiResponse<PublishPostResponse>.Ok(new PublishPostResponse
            {
                Success = post.Status == ContentPostStatus.Published,
                Post = dto,
                ErrorMessage = post.ErrorMessage
            }, post.Status == ContentPostStatus.Published ? "Published." : "Publish failed.");
        }
        catch (Exception ex)
        {
            return ApiResponse<PublishPostResponse>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<IReadOnlyList<SocialPostDto>>> GetPostsAsync(
        Guid userId,
        Guid? platformId = null,
        string? menuType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stores = string.IsNullOrWhiteSpace(menuType)
                ? _processData.AllStores()
                : [_processData.ForMenu(menuType)];

            var posts = new List<PostEntityBase>();
            foreach (var store in stores)
                posts.AddRange(await store.GetPostsByUserProfilesAsync(userId, platformId, cancellationToken));

            return ApiResponse<IReadOnlyList<SocialPostDto>>.Ok(
                posts.Select(p => Map(p, ProcessEntityNav.Platform(p)?.Code, ProcessEntityNav.Profile(p))).ToList());
        }
        catch (Exception ex)
        {
            return ApiResponse<IReadOnlyList<SocialPostDto>>.Fail(ex.Message);
        }
    }

    public async Task<ApiResponse<object>> DeletePostAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        try
        {
            var located = await ProcessStoreLocator.FindAsync(
                _processData,
                store => store.GetPostByIdAsync(postId, cancellationToken),
                cancellationToken);
            if (located is null)
                return ApiResponse<object>.Fail("Post not found.");

            var (store, post) = located.Value;
            var profile = await store.GetProfileByIdAsync(post.SocialProfileId, cancellationToken);
            var account = profile is null
                ? null
                : await store.GetSocialAccountByIdAsync(profile.SocialAccountId, cancellationToken);
            if (account is null || account.UserId != userId)
                return ApiResponse<object>.Fail("Post not found.");

            store.RemovePost(post);
            await store.SaveChangesAsync(cancellationToken);
            return ApiResponse<object>.Ok(new { }, "Post deleted.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    private static SocialPostDto Map(PostEntityBase post, string? platformCode, SocialProfileEntityBase? profile) => new()
    {
        Id = post.Id,
        SocialProfileId = post.SocialProfileId,
        PlatformId = post.PlatformId,
        PlatformCode = platformCode,
        ProfileName = profile?.Name,
        ProfileUsername = profile?.Username,
        ExternalPostId = post.ExternalPostId,
        Text = post.Text,
        Caption = post.Caption,
        Status = post.Status,
        LikeCount = post.LikeCount,
        CommentCount = post.CommentCount,
        ShareCount = post.ShareCount,
        ViewCount = post.ViewCount,
        PublishedAt = post.PublishedAt,
        ErrorMessage = post.ErrorMessage,
        CreatedAt = post.CreatedAt
    };
}
