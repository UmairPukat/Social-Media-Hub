using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Posts;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Application.Interfaces;

public interface IPostService
{
    Task<ApiResponse<PublishPostResponse>> CreateAndPublishAsync(Guid userId, CreatePostRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<IReadOnlyList<SocialPostDto>>> GetPostsAsync(
        Guid userId,
        Guid? platformId = null,
        string? menuType = null,
        CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> DeletePostAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
}
