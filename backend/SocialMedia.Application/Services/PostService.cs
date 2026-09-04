using System.Text.Json;
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
    private readonly IYouTubeService _youTubeService;

    public PostService(
        IProcessDataStoreFactory processData,
        IFacebookService facebookService,
        IInstagramService instagramService,
        IYouTubeService youTubeService)
    {
        _processData = processData;
        _facebookService = facebookService;
        _instagramService = instagramService;
        _youTubeService = youTubeService;
    }

    public async Task<ApiResponse<PublishPostResponse>> CreateAndPublishAsync(
        Guid userId,
        CreatePostRequest request,
        PublishMediaInput? media = null,
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
            var code = platform?.Code?.ToLowerInvariant() ?? string.Empty;

            var post = store.NewPost();
            post.SocialProfileId = profile.Id;
            post.PlatformId = account.PlatformId;
            post.Text = request.Content;
            post.Caption = request.Content;
            post.Type = ResolvePostType(code, request.MediaUrl, media);
            post.Status = ContentPostStatus.Draft;
            await store.AddPostAsync(post, cancellationToken);
            await store.SaveChangesAsync(cancellationToken);

            var auth = ProcessEntityNav.Auth(account)!;
            var accessToken = auth.AccessToken;
            if (string.IsNullOrWhiteSpace(accessToken))
                return ApiResponse<PublishPostResponse>.Fail("No access token is stored for this connection. Reconnect the account and try again.");

            try
            {
                switch (code)
                {
                    case "facebook":
                    {
                        var context = BuildMetaContext(profile, accessToken, InstagramConnectionType.FacebookLogin);
                        var result = await _facebookService.CreatePostAsync(
                            context,
                            request.Content,
                            request.MediaUrl,
                            media?.Stream,
                            media?.FileName,
                            media?.ContentType,
                            cancellationToken);
                        ApplyPublishSuccess(post, result.Id);
                        break;
                    }
                    case "instagram":
                    case "instagram_login":
                    {
                        var connectionType = code == "instagram_login"
                            ? InstagramConnectionType.InstagramLogin
                            : InstagramConnectionType.FacebookLogin;
                        var context = BuildMetaContext(profile, accessToken, connectionType);
                        var result = await _instagramService.CreatePostAsync(
                            context,
                            request.Content,
                            request.MediaUrl,
                            media?.Stream,
                            media?.FileName,
                            media?.ContentType,
                            cancellationToken);
                        ApplyPublishSuccess(post, result.Id);
                        break;
                    }
                    case "youtube":
                    {
                        if (media?.Stream is null)
                            throw new InvalidOperationException("YouTube uploads require a video file.");

                        var title = string.IsNullOrWhiteSpace(request.Title)
                            ? "Untitled video"
                            : request.Title.Trim();
                        var description = request.Content ?? string.Empty;
                        var privacy = NormalizeYouTubePrivacy(request.Visibility);

                        media.Stream.Position = 0;
                        var result = await _youTubeService.UploadVideoAsync(
                            accessToken,
                            title,
                            description,
                            media.Stream,
                            media.ContentType,
                            privacy,
                            cancellationToken);
                        ApplyPublishSuccess(post, result.VideoId);
                        post.Text = title;
                        post.Caption = description;
                        break;
                    }
                    default:
                        post.Status = ContentPostStatus.Failed;
                        post.ErrorMessage = $"Publishing is not supported for {code}.";
                        break;
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

    public async Task<ApiResponse<string>> GetPostPlatformCodeAsync(
        Guid userId,
        Guid postId,
        string menuType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var located = await ProcessStoreLocator.FindInMenuAsync(
                _processData,
                menuType,
                store => store.GetPostByIdAsync(postId, cancellationToken),
                cancellationToken);
            if (located is null)
                return ApiResponse<string>.Fail("Post not found.");

            var (store, post) = located.Value;
            var profile = await store.GetProfileByIdAsync(post.SocialProfileId, cancellationToken);
            var account = profile is null
                ? null
                : await store.GetSocialAccountByIdAsync(profile.SocialAccountId, cancellationToken);
            if (account is null || account.UserId != userId)
                return ApiResponse<string>.Fail("Post not found.");

            var platform = await store.GetPlatformByIdAsync(post.PlatformId, cancellationToken);
            return ApiResponse<string>.Ok(platform?.Code ?? string.Empty);
        }
        catch (Exception ex)
        {
            return ApiResponse<string>.Fail(ex.Message);
        }
    }

    private static MetaCallContext BuildMetaContext(
        SocialProfileEntityBase profile,
        string accessToken,
        InstagramConnectionType connectionType) => new()
    {
        AccessToken = accessToken,
        ProfileExternalId = profile.ExternalProfileId,
        PageExternalId = ReadJsonString(profile.MetadataJson, "pageId"),
        InstagramConnectionType = connectionType
    };

    private static ContentPostType ResolvePostType(string platformCode, string? mediaUrl, PublishMediaInput? media)
    {
        if (media?.Stream is not null)
            return platformCode == "youtube" ? ContentPostType.Video : ContentPostType.Image;

        if (string.IsNullOrWhiteSpace(mediaUrl))
            return ContentPostType.Text;

        var lower = mediaUrl.ToLowerInvariant();
        return lower.Contains(".mp4") || lower.Contains(".mov") || lower.Contains(".webm")
            ? ContentPostType.Video
            : ContentPostType.Image;
    }

    private static void ApplyPublishSuccess(PostEntityBase post, string externalId)
    {
        post.ExternalPostId = externalId;
        post.Status = ContentPostStatus.Published;
        post.PublishedAt = DateTime.UtcNow;
        post.ErrorMessage = null;
    }

    private static string NormalizeYouTubePrivacy(string? visibility)
        => (visibility ?? "public").Trim().ToLowerInvariant() switch
        {
            "private" => "private",
            "unlisted" => "unlisted",
            _ => "public"
        };

    private static string? ReadJsonString(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(propertyName, out var value)
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
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
