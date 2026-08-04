using SocialMedia.Domain.Common;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Domain.Entities;

/// <summary>
/// Tracks a background sync of posts/comments/messages for an account.
/// </summary>
public class SyncJob : BaseEntity
{
    public Guid SocialAccountId { get; set; }

    public SyncEntityType EntityType { get; set; }
    public string? Cursor { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public SyncJobStatus Status { get; set; } = SyncJobStatus.Pending;

    public int RecordsFetched { get; set; }
    public string? Error { get; set; }

    public SocialAccount? SocialAccount { get; set; }
}
