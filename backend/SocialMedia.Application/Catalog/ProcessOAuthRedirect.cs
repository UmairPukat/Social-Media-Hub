namespace SocialMedia.Application.Catalog;

/// <summary>
/// Builds OAuth callback URLs for each isolated process module.
/// </summary>
public static class ProcessOAuthRedirect
{
    public static string Resolve(string? menuType, string? configRedirectUri, string? backendBaseUrl)
    {
        var normalizedMenu = MenuTypes.Normalize(menuType);

        if (!string.IsNullOrWhiteSpace(configRedirectUri))
        {
            var trimmed = configRedirectUri.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return trimmed;

            if (!string.IsNullOrWhiteSpace(backendBaseUrl))
            {
                var path = trimmed.StartsWith('/') ? trimmed : $"/{trimmed}";
                return $"{backendBaseUrl.TrimEnd('/')}{path}";
            }
        }

        if (!string.IsNullOrWhiteSpace(backendBaseUrl))
            return $"{backendBaseUrl.TrimEnd('/')}{ProcessModules.CallbackRouteFor(normalizedMenu)}";

        return string.Empty;
    }

    public static string ResolveWhatsAppCallback(string? menuType, string? configRedirectUri, string? backendBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(configRedirectUri))
            return Resolve(menuType, configRedirectUri, backendBaseUrl);

        if (!string.IsNullOrWhiteSpace(backendBaseUrl))
            return $"{backendBaseUrl.TrimEnd('/')}{ProcessModules.WhatsAppCallbackRouteFor(menuType)}";

        return string.Empty;
    }

    public static string ResolveWhatsAppWebhook(string? menuType, string? configWebhookUrl, string? backendBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(configWebhookUrl))
        {
            var trimmed = configWebhookUrl.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return trimmed;

            if (!string.IsNullOrWhiteSpace(backendBaseUrl))
            {
                var path = trimmed.StartsWith('/') ? trimmed : $"/{trimmed}";
                return $"{backendBaseUrl.TrimEnd('/')}{path}";
            }
        }

        if (!string.IsNullOrWhiteSpace(backendBaseUrl))
            return $"{backendBaseUrl.TrimEnd('/')}{ProcessModules.WhatsAppWebhookRouteFor(menuType)}";

        return string.Empty;
    }

    public static bool SupportsAutoRedirect(string platformCode) =>
        platformCode is "facebook" or "instagram" or "instagram_login" or "whatsapp" or "youtube" or "tiktok";
}
