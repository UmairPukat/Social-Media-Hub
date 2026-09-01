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
    public async Task<IActionResult> CreateAndPublish([FromBody] CreatePostRequest model)
    {
        var response = await _postService.CreateAndPublishAsync(User.GetUserId(), model);
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
