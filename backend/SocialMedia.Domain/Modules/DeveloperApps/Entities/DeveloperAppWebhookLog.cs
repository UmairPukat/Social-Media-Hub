using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.DeveloperApps.Entities;

public class DeveloperAppWebhookLog : WebhookLogEntityBase
{
    public DeveloperAppPlatform? Platform { get; set; }
}
