using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Extensions;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers;

/// <summary>
/// Live public Facebook Page metadata for Meta App Review (Page Public Metadata Access).
/// Not used for connected Pages.
/// </summary>
[Authorize]
[Route("api/public-pages")]
[ApiController]
public class PublicPagesController : ControllerBase
{
    private readonly IPublicPagesService _publicPages;

    public PublicPagesController(IPublicPagesService publicPages)
    {
        _publicPages = publicPages;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] string? menuType,
        CancellationToken cancellationToken)
    {
        var response = await _publicPages.SearchAsync(User.GetUserId(), menuType, q, cancellationToken);
        return Ok(response);
    }
}
