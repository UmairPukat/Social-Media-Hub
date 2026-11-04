using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountConnections.Entities;

public class AccountConnectionComment : CommentEntityBase
{
    public AccountConnectionPost? Post { get; set; }
    public AccountConnectionComment? ParentComment { get; set; }
    public ICollection<AccountConnectionComment> Replies { get; set; } = new List<AccountConnectionComment>();
}
