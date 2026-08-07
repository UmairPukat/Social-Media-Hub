using SocialMedia.Application.DTOs.Inbox;

namespace SocialMedia.Application.Interfaces;

/// <summary>
/// Pushes new inbox comments/messages to connected Angular clients over SignalR.
/// </summary>
public interface IInboxRealtimeNotifier
{
    Task NotifyInboxItemAsync(Guid userId, InboxItemDto item, CancellationToken cancellationToken = default);
}
