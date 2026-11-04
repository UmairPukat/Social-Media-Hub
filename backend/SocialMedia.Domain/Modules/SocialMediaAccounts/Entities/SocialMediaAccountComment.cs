using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.SocialMediaAccounts.Entities;

public class SocialMediaAccountComment : CommentEntityBase
{
    public SocialMediaAccountPost? Post { get; set; }
    public SocialMediaAccountComment? ParentComment { get; set; }
    public ICollection<SocialMediaAccountComment> Replies { get; set; } = new List<SocialMediaAccountComment>();
}
