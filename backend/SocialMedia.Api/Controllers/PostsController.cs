using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Extensions;
using SocialMedia.Application.DTOs.Posts;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers;

[Authorize]
[Route("api/[controller]/[action]")]
[ApiController]
public class PostsController : ControllerBase
{
    private readonly IPostService _postService;

    public PostsController(IPostService postService)
    {
        _postService = postService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPosts(Guid? platformId = null)
    {
        var response = await _postService.GetPostsAsync(User.GetUserId(), platformId);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAndPublish([FromBody] CreatePostRequest model)
    {
        var response = await _postService.CreateAndPublishAsync(User.GetUserId(), model);
        return Ok(response);
    }

    [HttpDelete]
    public async Task<IActionResult> DeletePost(Guid id)
    {
        var response = await _postService.DeletePostAsync(User.GetUserId(), id);
        return Ok(response);
    }
}
