using SocialMedia.Domain.Common;

namespace SocialMedia.Domain.Modules.Common.Entities;

public abstract class PlatformEntityBase : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public bool IsActive { get; set; } = true;
}
