using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AppConnections.Entities;

public class AppConnectionPost : PostEntityBase
{
    public AppConnectionSocialProfile? SocialProfile { get; set; }
    public AppConnectionPlatform? Platform { get; set; }
    public ICollection<AppConnectionMedia> MediaItems { get; set; } = new List<AppConnectionMedia>();
    public ICollection<AppConnectionComment> Comments { get; set; } = new List<AppConnectionComment>();
}
