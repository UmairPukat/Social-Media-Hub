using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AppConnections.Entities;

public class AppConnectionWebhookEvent : WebhookEventEntityBase
{
    public AppConnectionPlatform? Platform { get; set; }
}
