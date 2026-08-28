using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.DeveloperApps.Entities;

public class DeveloperAppMedia : MediaEntityBase
{
    public DeveloperAppPost? Post { get; set; }
}
