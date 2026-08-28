using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.DeveloperApps.Entities;

public class DeveloperAppSocialProfile : SocialProfileEntityBase
{
    public DeveloperAppSocialAccount? SocialAccount { get; set; }
    public ICollection<DeveloperAppPost> Posts { get; set; } = new List<DeveloperAppPost>();
    public ICollection<DeveloperAppConversation> Conversations { get; set; } = new List<DeveloperAppConversation>();
}
