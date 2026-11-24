using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Extensions;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers.Common;

public abstract class ProcessAnalyticsControllerBase : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly IReadInsightsService _readInsights;

    protected ProcessAnalyticsControllerBase(IDashboardService dashboardService, IReadInsightsService readInsights)
    {
        _dashboardService = dashboardService;
        _readInsights = readInsights;
    }

    protected abstract string MenuType { get; }

    [HttpGet("analytics/summary")]
    public async Task<IActionResult> GetSummary()
    {
        var response = await _dashboardService.GetSummaryForProcessAsync(User.GetUserId(), MenuType);
        return Ok(response);
    }

    [HttpGet("read-insights")]
    public async Task<IActionResult> GetReadInsights(
        [FromQuery] int days = 28,
        [FromQuery] string? platform = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _readInsights.GetAsync(User.GetUserId(), MenuType, days, platform, cancellationToken);
        return Ok(response);
    }
}
