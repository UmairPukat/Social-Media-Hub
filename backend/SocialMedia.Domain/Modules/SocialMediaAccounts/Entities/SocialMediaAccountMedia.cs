using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.SocialMediaAccounts.Entities;

public class SocialMediaAccountMedia : MediaEntityBase
{
    public SocialMediaAccountPost? Post { get; set; }
}
