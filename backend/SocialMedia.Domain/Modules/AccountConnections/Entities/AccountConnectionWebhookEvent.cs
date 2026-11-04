using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountConnections.Entities;

public class AccountConnectionWebhookEvent : WebhookEventEntityBase
{
    public AccountConnectionPlatform? Platform { get; set; }
}
