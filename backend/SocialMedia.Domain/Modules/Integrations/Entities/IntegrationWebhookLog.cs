using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.Integrations.Entities;

public class IntegrationWebhookLog : WebhookLogEntityBase
{
    public IntegrationPlatform? Platform { get; set; }
}
