using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Extensions;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers.Common;

public abstract class ProcessYouTubeSyncControllerBase : ControllerBase
{
    private readonly IYouTubeSyncService _youTubeSync;

    protected ProcessYouTubeSyncControllerBase(IYouTubeSyncService youTubeSync)
    {
        _youTubeSync = youTubeSync;
    }

    protected abstract string MenuType { get; }

    [HttpPost("sync/youtube/posts")]
    public async Task<IActionResult> SyncPosts([FromQuery] string? platformCode = null)
    {
        var response = await _youTubeSync.SyncPostsAsync(User.GetUserId(), MenuType, platformCode);
        return Ok(response);
    }

    [HttpPost("sync/youtube/comments")]
    public async Task<IActionResult> SyncComments([FromQuery] string? platformCode = null)
    {
        var response = await _youTubeSync.SyncCommentsAsync(User.GetUserId(), MenuType, platformCode);
        return Ok(response);
    }

    [HttpPost("sync/youtube/statistics")]
    public async Task<IActionResult> SyncStatistics([FromQuery] string? platformCode = null)
    {
        var response = await _youTubeSync.SyncStatisticsAsync(User.GetUserId(), MenuType, platformCode);
        return Ok(response);
    }
}
