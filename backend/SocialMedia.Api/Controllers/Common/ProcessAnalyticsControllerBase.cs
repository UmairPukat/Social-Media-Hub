using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Extensions;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers.Common;

public abstract class ProcessAnalyticsControllerBase : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    protected ProcessAnalyticsControllerBase(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    protected abstract string MenuType { get; }

    [HttpGet("analytics/summary")]
    public async Task<IActionResult> GetSummary()
    {
        var response = await _dashboardService.GetSummaryForProcessAsync(User.GetUserId(), MenuType);
        return Ok(response);
    }
}
