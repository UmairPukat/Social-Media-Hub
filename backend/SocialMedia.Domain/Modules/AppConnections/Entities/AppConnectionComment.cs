using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AppConnections.Entities;

public class AppConnectionComment : CommentEntityBase
{
    public AppConnectionPost? Post { get; set; }
    public AppConnectionComment? ParentComment { get; set; }
    public ICollection<AppConnectionComment> Replies { get; set; } = new List<AppConnectionComment>();
}
