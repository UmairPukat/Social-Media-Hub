using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Extensions;
using SocialMedia.Application.DTOs.Posts;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers.Common;

public abstract class ProcessPostsControllerBase : ControllerBase
{
    private readonly IPostService _postService;
    private readonly IYouTubeSyncService _youTubeSync;

    protected ProcessPostsControllerBase(IPostService postService, IYouTubeSyncService youTubeSync)
    {
        _postService = postService;
        _youTubeSync = youTubeSync;
    }

    protected abstract string MenuType { get; }

    [HttpGet("posts")]
    public async Task<IActionResult> GetPosts(Guid? platformId = null)
    {
        var response = await _postService.GetPostsAsync(User.GetUserId(), platformId, MenuType);
        return Ok(response);
    }

    [HttpPost("posts")]
    [RequestSizeLimit(524_288_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 524_288_000)]
    public async Task<IActionResult> CreateAndPublish([FromForm] CreatePostForm form)
    {
        PublishMediaInput? media = null;
        if (form.MediaFile is { Length: > 0 })
        {
            media = new PublishMediaInput
            {
                Stream = form.MediaFile.OpenReadStream(),
                FileName = form.MediaFile.FileName,
                ContentType = form.MediaFile.ContentType ?? "application/octet-stream"
            };
        }

        var request = new CreatePostRequest
        {
            SocialProfileId = form.SocialProfileId,
            Content = form.Content ?? string.Empty,
            MediaUrl = string.IsNullOrWhiteSpace(form.MediaUrl) ? null : form.MediaUrl.Trim(),
            Title = string.IsNullOrWhiteSpace(form.Title) ? null : form.Title.Trim(),
            Visibility = string.IsNullOrWhiteSpace(form.Visibility) ? null : form.Visibility.Trim()
        };

        var response = await _postService.CreateAndPublishAsync(User.GetUserId(), request, media);
        return Ok(response);
    }

    [HttpDelete("posts/{id:guid}")]
    public async Task<IActionResult> DeletePost(Guid id)
    {
        var response = await _postService.DeletePostAsync(User.GetUserId(), id);
        return Ok(response);
    }

    [HttpGet("posts/{id:guid}/statistics")]
    public async Task<IActionResult> GetPostStatistics(Guid id, [FromQuery] bool refresh = false)
    {
        var response = await _youTubeSync.GetPostStatisticsAsync(User.GetUserId(), id, MenuType, refresh);
        return Ok(response);
    }
}
