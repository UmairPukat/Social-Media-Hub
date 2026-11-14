using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Domain.Modules.AccountManagement.Entities;

public class AccountManagementWebhookLog : WebhookLogEntityBase
{
    public AccountManagementPlatform? Platform { get; set; }
}
