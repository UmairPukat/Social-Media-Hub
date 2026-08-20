using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Application.DTOs.EnvironmentVariables;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers;

[Authorize]
[Route("api/[controller]/[action]")]
[ApiController]
public class EnvironmentVariablesController : ControllerBase
{
    private readonly IEnvironmentVariableService _service;

    public EnvironmentVariablesController(IEnvironmentVariableService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetByScope(string scope, CancellationToken cancellationToken)
    {
        var response = await _service.GetByScopeAsync(scope, cancellationToken);
        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetById(Guid id, bool reveal = false, CancellationToken cancellationToken = default)
    {
        var response = await _service.GetByIdAsync(id, reveal, cancellationToken);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertEnvironmentVariableRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.CreateAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPut]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertEnvironmentVariableRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(response);
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var response = await _service.DeleteAsync(id, cancellationToken);
        return Ok(response);
    }
}
