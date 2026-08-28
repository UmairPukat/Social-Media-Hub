using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.DeveloperApps.Entities;

public class DeveloperAppPost : PostEntityBase
{
    public DeveloperAppSocialProfile? SocialProfile { get; set; }
    public DeveloperAppPlatform? Platform { get; set; }
    public ICollection<DeveloperAppMedia> MediaItems { get; set; } = new List<DeveloperAppMedia>();
    public ICollection<DeveloperAppComment> Comments { get; set; } = new List<DeveloperAppComment>();
}
