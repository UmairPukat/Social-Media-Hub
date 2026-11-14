using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountHub.Entities;

public class AccountHubWebhookEvent : WebhookEventEntityBase
{
    public AccountHubPlatform? Platform { get; set; }
}
