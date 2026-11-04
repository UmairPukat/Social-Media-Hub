using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.SocialMediaAccounts.Entities;

public class SocialMediaAccountSocialProfile : SocialProfileEntityBase
{
    public SocialMediaAccountSocialAccount? SocialAccount { get; set; }
    public ICollection<SocialMediaAccountPost> Posts { get; set; } = new List<SocialMediaAccountPost>();
    public ICollection<SocialMediaAccountConversation> Conversations { get; set; } = new List<SocialMediaAccountConversation>();
}
