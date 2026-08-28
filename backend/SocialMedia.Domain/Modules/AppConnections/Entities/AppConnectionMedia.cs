using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AppConnections.Entities;

public class AppConnectionMedia : MediaEntityBase
{
    public AppConnectionPost? Post { get; set; }
}
