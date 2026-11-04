using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.SocialMediaAccounts.Entities;

public class SocialMediaAccountPost : PostEntityBase
{
    public SocialMediaAccountSocialProfile? SocialProfile { get; set; }
    public SocialMediaAccountPlatform? Platform { get; set; }
    public ICollection<SocialMediaAccountMedia> MediaItems { get; set; } = new List<SocialMediaAccountMedia>();
    public ICollection<SocialMediaAccountComment> Comments { get; set; } = new List<SocialMediaAccountComment>();
}
