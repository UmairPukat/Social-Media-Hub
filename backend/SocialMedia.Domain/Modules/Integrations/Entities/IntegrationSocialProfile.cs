using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.Integrations.Entities;

public class IntegrationSocialProfile : SocialProfileEntityBase
{
    public IntegrationSocialAccount? SocialAccount { get; set; }
    public ICollection<IntegrationPost> Posts { get; set; } = new List<IntegrationPost>();
    public ICollection<IntegrationConversation> Conversations { get; set; } = new List<IntegrationConversation>();
}
