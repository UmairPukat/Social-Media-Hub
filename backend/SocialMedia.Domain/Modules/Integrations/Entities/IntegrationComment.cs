using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.Integrations.Entities;

public class IntegrationComment : CommentEntityBase
{
    public IntegrationPost? Post { get; set; }
    public IntegrationComment? ParentComment { get; set; }
    public ICollection<IntegrationComment> Replies { get; set; } = new List<IntegrationComment>();
}
