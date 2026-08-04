using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Extensions;
using SocialMedia.Application.DTOs.Inbox;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers;

[Authorize]
[Route("api/[controller]/[action]")]
[ApiController]
public class InboxController : ControllerBase
{
    private readonly IInboxService _inboxService;

    public InboxController(IInboxService inboxService)
    {
        _inboxService = inboxService;
    }

    [HttpGet]
    public async Task<IActionResult> GetInbox(string? platformCode = null, string? itemKind = null)
    {
        var filter = new InboxFilterRequest { PlatformCode = platformCode, ItemKind = itemKind };
        var response = await _inboxService.GetInboxAsync(User.GetUserId(), filter);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> ReplyToComment(Guid id, [FromBody] ReplyCommentRequest model)
    {
        var response = await _inboxService.ReplyToCommentAsync(User.GetUserId(), id, model);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> HideComment(Guid id, [FromBody] HideCommentRequest model)
    {
        var response = await _inboxService.HideCommentAsync(User.GetUserId(), id, model);
        return Ok(response);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteComment(Guid id)
    {
        var response = await _inboxService.DeleteCommentAsync(User.GetUserId(), id);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> ReplyToMessage(Guid id, [FromBody] ReplyMessageRequest model)
    {
        var response = await _inboxService.ReplyToMessageAsync(User.GetUserId(), id, model);
        return Ok(response);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteMessage(Guid id)
    {
        var response = await _inboxService.DeleteMessageAsync(User.GetUserId(), id);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var response = await _inboxService.MarkReadAsync(User.GetUserId(), id);
        return Ok(response);
    }
}
