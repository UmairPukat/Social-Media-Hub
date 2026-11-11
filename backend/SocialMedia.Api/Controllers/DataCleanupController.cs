using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/data-cleanup")]
[ApiController]
public class DataCleanupController : ControllerBase
{
    private readonly IModuleDataPurgeService _purgeService;

    public DataCleanupController(IModuleDataPurgeService purgeService)
    {
        _purgeService = purgeService;
    }

    [HttpPost("{menuType}")]
    public async Task<IActionResult> Purge(string menuType, CancellationToken cancellationToken)
    {
        var response = await _purgeService.PurgeAsync(menuType, cancellationToken);
        return Ok(response);
    }
}
