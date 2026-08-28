using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.Integrations.Entities;

public class IntegrationPost : PostEntityBase
{
    public IntegrationSocialProfile? SocialProfile { get; set; }
    public IntegrationPlatform? Platform { get; set; }
    public ICollection<IntegrationMedia> MediaItems { get; set; } = new List<IntegrationMedia>();
    public ICollection<IntegrationComment> Comments { get; set; } = new List<IntegrationComment>();
}
