using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SocialMedia.Api.Hubs;

/// <summary>
/// Real-time inbox hub. Clients join a private group for their user id.
/// </summary>
[Authorize]
public class InboxHub : Hub
{
    public static string UserGroup(Guid userId) => $"user:{userId:D}";

    public override async Task OnConnectedAsync()
    {
        var userId = ResolveUserId();
        if (userId.HasValue)
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId.Value));

        await base.OnConnectedAsync();
    }

    private Guid? ResolveUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
