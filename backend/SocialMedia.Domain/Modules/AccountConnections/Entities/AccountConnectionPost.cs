using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountConnections.Entities;

public class AccountConnectionPost : PostEntityBase
{
    public AccountConnectionSocialProfile? SocialProfile { get; set; }
    public AccountConnectionPlatform? Platform { get; set; }
    public ICollection<AccountConnectionMedia> MediaItems { get; set; } = new List<AccountConnectionMedia>();
    public ICollection<AccountConnectionComment> Comments { get; set; } = new List<AccountConnectionComment>();
}
