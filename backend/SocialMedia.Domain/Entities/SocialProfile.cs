using SocialMedia.Domain.Common;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Domain.Entities;

/// <summary>
/// Facebook Page / Instagram Business / WhatsApp Phone under a SocialAccount.
/// </summary>
public class SocialProfile : BaseEntity
{
    public Guid SocialAccountId { get; set; }

    public string ExternalProfileId { get; set; } = string.Empty;
    public ProfileType ProfileType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? ProfileImage { get; set; }
    public string? MetadataJson { get; set; }

    public SocialAccount? SocialAccount { get; set; }
    public ICollection<Post> Posts { get; set; } = new List<Post>();
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
}
