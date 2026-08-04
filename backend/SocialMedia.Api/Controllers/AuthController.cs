using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Application.DTOs.Auth;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers;

[AllowAnonymous]
[Route("api/[controller]/[action]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost]
    public async Task<IActionResult> Login([FromBody] LoginRequest model)
    {
        var response = await _authService.LoginAsync(model);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Signup([FromBody] SignupRequest model)
    {
        var response = await _authService.SignupAsync(model);
        return Ok(response);
    }
}
