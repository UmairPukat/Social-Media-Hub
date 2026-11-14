using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountHub.Entities;

public class AccountHubPost : PostEntityBase
{
    public AccountHubSocialProfile? SocialProfile { get; set; }
    public AccountHubPlatform? Platform { get; set; }
    public ICollection<AccountHubMedia> MediaItems { get; set; } = new List<AccountHubMedia>();
    public ICollection<AccountHubComment> Comments { get; set; } = new List<AccountHubComment>();
}
