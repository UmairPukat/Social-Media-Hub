using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountHub.Entities;

public class AccountHubComment : CommentEntityBase
{
    public AccountHubPost? Post { get; set; }
    public AccountHubComment? ParentComment { get; set; }
    public ICollection<AccountHubComment> Replies { get; set; } = new List<AccountHubComment>();
}
