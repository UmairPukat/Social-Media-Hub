using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Extensions;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers;

[Authorize]
[Route("api/[controller]/[action]")]
[ApiController]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSummary()
    {
        var response = await _dashboardService.GetSummaryAsync(User.GetUserId());
        return Ok(response);
    }
}
