using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.Integrations.Entities;

public class IntegrationWebhookEvent : WebhookEventEntityBase
{
    public IntegrationPlatform? Platform { get; set; }
}
