using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountManagement.Entities;

public class AccountManagementWebhookEvent : WebhookEventEntityBase
{
    public AccountManagementPlatform? Platform { get; set; }
}
