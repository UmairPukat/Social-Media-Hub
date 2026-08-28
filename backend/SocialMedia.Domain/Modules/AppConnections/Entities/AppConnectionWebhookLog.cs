using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AppConnections.Entities;

public class AppConnectionWebhookLog : WebhookLogEntityBase
{
    public AppConnectionPlatform? Platform { get; set; }
}
