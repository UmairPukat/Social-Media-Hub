using SocialMedia.Domain.Common;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Domain.Modules.Common.Entities;

public abstract class SyncJobEntityBase : BaseEntity
{
    public Guid SocialAccountId { get; set; }
    public SyncEntityType EntityType { get; set; }
    public string? Cursor { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public SyncJobStatus Status { get; set; } = SyncJobStatus.Pending;
    public int RecordsFetched { get; set; }
    public string? Error { get; set; }
}
