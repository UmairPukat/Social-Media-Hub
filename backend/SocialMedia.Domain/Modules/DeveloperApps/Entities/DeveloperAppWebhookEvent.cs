using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.DeveloperApps.Entities;

public class DeveloperAppWebhookEvent : WebhookEventEntityBase
{
    public DeveloperAppPlatform? Platform { get; set; }
}
