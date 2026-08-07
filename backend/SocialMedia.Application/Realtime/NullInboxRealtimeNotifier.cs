using SocialMedia.Application.Interfaces;

namespace SocialMedia.Application.Realtime;

/// <summary>
/// No-op notifier used when SignalR is not yet registered (e.g. unit tests).
/// </summary>
public sealed class NullInboxRealtimeNotifier : IInboxRealtimeNotifier
{
    public Task NotifyInboxItemAsync(Guid userId, Application.DTOs.Inbox.InboxItemDto item, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
