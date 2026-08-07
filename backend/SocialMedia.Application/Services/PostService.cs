using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Posts;
using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Interfaces;

namespace SocialMedia.Application.Services;

public class PostService : IPostService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFacebookService _facebookService;
    private readonly IInstagramService _instagramService;

    public PostService(IUnitOfWork unitOfWork, IFacebookService facebookService, IInstagramService instagramService)
    {
        _unitOfWork = unitOfWork;
        _facebookService = facebookService;
        _instagramService = instagramService;
    }

    public async Task<ApiResponse<PublishPostResponse>> CreateAndPublishAsync(Guid userId, CreatePostRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var profile = await _unitOfWork.SocialProfiles.GetByIdAsync(request.SocialProfileId, cancellationToken);
            if (profile is null)
                return ApiResponse<PublishPostResponse>.Fail("Social profile not found.");

            var account = await _unitOfWork.SocialAccounts.GetWithAuthAndProfilesAsync(profile.SocialAccountId, cancellationToken);
            if (account is null || account.UserId != userId || account.Auth is null)
                return ApiResponse<PublishPostResponse>.Fail("Connected account not found.");

            var platform = await _unitOfWork.Platforms.GetByIdAsync(account.PlatformId, cancellationToken);
            var post = new Post
            {
                SocialProfileId = profile.Id,
                PlatformId = account.PlatformId,
                Text = request.Content,
                Caption = request.Content,
                Type = string.IsNullOrWhiteSpace(request.MediaUrl) ? ContentPostType.Text : ContentPostType.Image,
                Status = ContentPostStatus.Draft
            };
            await _unitOfWork.Posts.AddAsync(post, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var context = new MetaCallContext
            {
                AccessToken = account.Auth.AccessToken,
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

            if (!string.IsNullOrWhiteSpace(request.MediaUrl))
            {
                await _unitOfWork.Posts.SaveChangesAsync(cancellationToken);
                // Media row via context — add through Posts navigation by reloading not needed; use unit of work Posts only.
            }

            _unitOfWork.Posts.Update(post);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var dto = Map(post, platform?.Code);
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

    public async Task<ApiResponse<IReadOnlyList<SocialPostDto>>> GetPostsAsync(Guid userId, Guid? platformId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var posts = await _unitOfWork.Posts.GetByUserProfilesAsync(userId, platformId, cancellationToken);
            return ApiResponse<IReadOnlyList<SocialPostDto>>.Ok(posts.Select(p => Map(p, p.Platform?.Code)).ToList());
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
            var post = await _unitOfWork.Posts.GetByIdAsync(postId, cancellationToken);
            if (post is null)
                return ApiResponse<object>.Fail("Post not found.");

            var profile = await _unitOfWork.SocialProfiles.GetByIdAsync(post.SocialProfileId, cancellationToken);
            var account = profile is null ? null : await _unitOfWork.SocialAccounts.GetByIdAsync(profile.SocialAccountId, cancellationToken);
            if (account is null || account.UserId != userId)
                return ApiResponse<object>.Fail("Post not found.");

            _unitOfWork.Posts.Remove(post);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponse<object>.Ok(new { }, "Post deleted.");
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail(ex.Message);
        }
    }

    private static SocialPostDto Map(Post post, string? platformCode) => new()
    {
        Id = post.Id,
        SocialProfileId = post.SocialProfileId,
        PlatformId = post.PlatformId,
        PlatformCode = platformCode,
        ProfileName = post.SocialProfile?.Name,
        ProfileUsername = post.SocialProfile?.Username,
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
