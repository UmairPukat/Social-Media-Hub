using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountHub.Entities;

public class AccountHubMedia : MediaEntityBase
{
    public AccountHubPost? Post { get; set; }
}
