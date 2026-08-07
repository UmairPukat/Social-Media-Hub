using Microsoft.AspNetCore.SignalR;
using SocialMedia.Application.DTOs.Inbox;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Hubs;

public class InboxRealtimeNotifier : IInboxRealtimeNotifier
{
    private readonly IHubContext<InboxHub> _hub;

    public InboxRealtimeNotifier(IHubContext<InboxHub> hub)
    {
        _hub = hub;
    }

    public Task NotifyInboxItemAsync(Guid userId, InboxItemDto item, CancellationToken cancellationToken = default)
        => _hub.Clients.Group(InboxHub.UserGroup(userId))
            .SendAsync("inboxItem", item, cancellationToken);
}
