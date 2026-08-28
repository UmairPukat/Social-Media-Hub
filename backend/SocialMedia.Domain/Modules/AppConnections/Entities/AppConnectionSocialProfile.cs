using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AppConnections.Entities;

public class AppConnectionSocialProfile : SocialProfileEntityBase
{
    public AppConnectionSocialAccount? SocialAccount { get; set; }
    public ICollection<AppConnectionPost> Posts { get; set; } = new List<AppConnectionPost>();
    public ICollection<AppConnectionConversation> Conversations { get; set; } = new List<AppConnectionConversation>();
}
