using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Extensions;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers.Common;

public abstract class ProcessTikTokSyncControllerBase : ControllerBase
{
    private readonly ITikTokSyncService _tikTokSync;

    protected ProcessTikTokSyncControllerBase(ITikTokSyncService tikTokSync)
    {
        _tikTokSync = tikTokSync;
    }

    protected abstract string MenuType { get; }

    [HttpPost("sync/tiktok/posts")]
    public async Task<IActionResult> SyncPosts([FromQuery] string? platformCode = null)
    {
        var response = await _tikTokSync.SyncPostsAsync(User.GetUserId(), MenuType, platformCode);
        return Ok(response);
    }

    [HttpPost("sync/tiktok/statistics")]
    public async Task<IActionResult> SyncStatistics([FromQuery] string? platformCode = null)
    {
        var response = await _tikTokSync.SyncStatisticsAsync(User.GetUserId(), MenuType, platformCode);
        return Ok(response);
    }
}
