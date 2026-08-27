using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Infrastructure.Meta;

/// <summary>
/// Ensures webhook deliveries only create inbox rows for profiles owned by a connected
/// account in the same process module that received the delivery.
/// </summary>
internal static class WebhookProfileGuard
{
    public static bool CanProcess(
        SocialProfile profile,
        SocialAccount account,
        WebhookEvent webhookEvent,
        WebhookProcessResult result)
    {
        if (account.Status != SocialAccountStatus.Connected)
        {
            result.Skip($"Account '{account.Id}' is not connected — message not stored.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(webhookEvent.MenuType))
            return true;

        var module = webhookEvent.MenuType.Trim();
        var accountMatches = string.Equals(account.MenuType, module, StringComparison.OrdinalIgnoreCase);
        var profileMatches = string.Equals(profile.MenuType, module, StringComparison.OrdinalIgnoreCase);

        if (!accountMatches && !profileMatches)
        {
            result.Skip(
                $"Entry ignored — connected in module '{account.MenuType}' but webhook is for '{module}'.");
            return false;
        }

        return true;
    }

    /// <summary>Meta's test tool sends placeholder entry ids; never attach those to a real inbox.</summary>
    public static bool IsTestDeliveryId(string? id)
        => string.IsNullOrWhiteSpace(id) || id == "0";
}
