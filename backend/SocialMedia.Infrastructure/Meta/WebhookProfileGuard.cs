using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Infrastructure.Meta;

/// <summary>
/// Ensures webhook deliveries only create inbox rows for profiles owned by a connected
/// account in the process module that received the delivery.
/// </summary>
internal static class WebhookProfileGuard
{
    public static bool CanProcess(
        SocialProfileEntityBase profile,
        SocialAccountEntityBase account,
        string? menuType,
        WebhookProcessResult result)
    {
        if (account.Status != SocialAccountStatus.Connected)
        {
            result.Skip($"Account '{account.Id}' is not connected — message not stored.");
            return false;
        }

        return true;
    }

    /// <summary>Meta's test tool sends placeholder entry ids; never attach those to a real inbox.</summary>
    public static bool IsTestDeliveryId(string? id)
        => string.IsNullOrWhiteSpace(id) || id == "0";
}
