using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountConnections.Entities;

public class AccountConnectionWebhookLog : WebhookLogEntityBase
{
    public AccountConnectionPlatform? Platform { get; set; }
}
