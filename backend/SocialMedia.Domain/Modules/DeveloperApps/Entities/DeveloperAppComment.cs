using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.DeveloperApps.Entities;

public class DeveloperAppComment : CommentEntityBase
{
    public DeveloperAppPost? Post { get; set; }
    public DeveloperAppComment? ParentComment { get; set; }
    public ICollection<DeveloperAppComment> Replies { get; set; } = new List<DeveloperAppComment>();
}
