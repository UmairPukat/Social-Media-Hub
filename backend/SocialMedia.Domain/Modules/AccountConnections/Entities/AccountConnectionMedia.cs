using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountConnections.Entities;

public class AccountConnectionMedia : MediaEntityBase
{
    public AccountConnectionPost? Post { get; set; }
}
