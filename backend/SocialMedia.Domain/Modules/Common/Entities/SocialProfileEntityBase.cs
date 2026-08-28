using SocialMedia.Domain.Common;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Domain.Modules.Common.Entities;

public abstract class SocialProfileEntityBase : BaseEntity
{
    public Guid SocialAccountId { get; set; }
    public string ExternalProfileId { get; set; } = string.Empty;
    public ProfileType ProfileType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? ProfileImage { get; set; }
    public string? MetadataJson { get; set; }
}
