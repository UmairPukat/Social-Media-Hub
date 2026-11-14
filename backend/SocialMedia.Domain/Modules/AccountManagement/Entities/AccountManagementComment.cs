using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountManagement.Entities;

public class AccountManagementComment : CommentEntityBase
{
    public AccountManagementPost? Post { get; set; }
    public AccountManagementComment? ParentComment { get; set; }
    public ICollection<AccountManagementComment> Replies { get; set; } = new List<AccountManagementComment>();
}
