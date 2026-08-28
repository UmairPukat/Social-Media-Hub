using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.Integrations.Entities;

public class IntegrationMedia : MediaEntityBase
{
    public IntegrationPost? Post { get; set; }
}
