using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Extensions;
using SocialMedia.Application.DTOs.Inbox;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers.Common;

public abstract class ProcessInboxControllerBase : ControllerBase
{
    private readonly IInboxService _inboxService;

    protected ProcessInboxControllerBase(IInboxService inboxService)
    {
        _inboxService = inboxService;
    }

    protected abstract string MenuType { get; }

    [HttpGet("inbox")]
    public async Task<IActionResult> GetInbox(string? platformCode = null, string? itemKind = null)
    {
        var filter = new InboxFilterRequest
        {
            PlatformCode = platformCode,
            ItemKind = itemKind,
            MenuType = MenuType
        };
        var response = await _inboxService.GetInboxAsync(User.GetUserId(), filter);
        return Ok(response);
    }

    [HttpPost("inbox/comments/{id:guid}/reply")]
    public async Task<IActionResult> ReplyToComment(Guid id, [FromBody] ReplyCommentRequest model)
    {
        model.MenuType ??= MenuType;
        var response = await _inboxService.ReplyToCommentAsync(User.GetUserId(), id, model);
        return Ok(response);
    }

    [HttpPost("inbox/messages/{id:guid}/reply")]
    public async Task<IActionResult> ReplyToMessage(Guid id, [FromBody] ReplyMessageRequest model)
    {
        model.MenuType ??= MenuType;
        var response = await _inboxService.ReplyToMessageAsync(User.GetUserId(), id, model);
        return Ok(response);
    }

    [HttpPost("inbox/comments/{id:guid}/hide")]
    public async Task<IActionResult> HideComment(Guid id, [FromBody] HideCommentRequest model)
    {
        var response = await _inboxService.HideCommentAsync(User.GetUserId(), id, model);
        return Ok(response);
    }

    [HttpDelete("inbox/comments/{id:guid}")]
    public async Task<IActionResult> DeleteComment(Guid id)
    {
        var response = await _inboxService.DeleteCommentAsync(User.GetUserId(), id);
        return Ok(response);
    }

    [HttpDelete("inbox/messages/{id:guid}")]
    public async Task<IActionResult> DeleteMessage(Guid id)
    {
        var response = await _inboxService.DeleteMessageAsync(User.GetUserId(), id);
        return Ok(response);
    }

    [HttpPost("inbox/conversations/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var response = await _inboxService.MarkReadAsync(User.GetUserId(), id);
        return Ok(response);
    }
}
