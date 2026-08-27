namespace SocialMedia.Application.Catalog;

public record PlatformDefinition(
    Guid Id,
    string Code,
    string Name,
    string Icon,
    string Category,
    string CategoryLabel,
    int SortOrder,
    string Description,
    bool SupportsComments,
    bool SupportsMessages,
    bool SupportsPosts,
    bool CanConnect);

/// <summary>
/// Full Integrations catalog grouped by category.
/// Meta OAuth connect is enabled for Facebook, Instagram (Facebook Login),
/// Instagram Login, and WhatsApp only.
/// </summary>
public static class PlatformCatalog
{
    // Preserve legacy Meta / social GUIDs already used in databases.
    public static readonly Guid FacebookId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid InstagramId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid InstagramLoginId = Guid.Parse("22222222-2222-2222-2222-222222222223");
    public static readonly Guid WhatsAppId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid YouTubeId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid LinkedInId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid TikTokId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    public static readonly Guid TwitterId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    public static readonly IReadOnlyList<PlatformDefinition> All = Build();

    public static readonly IReadOnlyList<(string Id, string Label)> Categories =
    [
        ("social", "Social"),
        ("communication", "Communication"),
        ("commerce", "Commerce"),
        ("crm", "CRM"),
        ("calendar", "Calendar"),
        ("storage", "Storage"),
        ("payment", "Payment"),
        ("ai", "AI")
    ];

    public static PlatformDefinition? Find(string code) =>
        All.FirstOrDefault(p => p.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<PlatformDefinition> Build()
    {
        var list = new List<PlatformDefinition>();
        var order = 0;

        void Add(
            string category,
            string categoryLabel,
            Guid id,
            string code,
            string name,
            string icon,
            string description,
            bool comments = false,
            bool messages = false,
            bool posts = false,
            bool canConnect = false)
        {
            list.Add(new PlatformDefinition(
                id,
                code,
                name,
                icon,
                category,
                categoryLabel,
                order++,
                description,
                comments,
                messages,
                posts,
                canConnect));
        }

        // Social
        Add("social", "Social", FacebookId, "facebook", "Facebook", "facebook",
            "Publish posts, manage Page comments, and reply to Messenger.", true, true, true, true);
        Add("social", "Social", InstagramId, "instagram", "Instagram", "instagram",
            "Publish media and manage comments/DMs via Facebook Login.", true, true, true, true);
        Add("social", "Social", InstagramLoginId, "instagram_login", "Instagram Login", "instagram",
            "Connect a professional Instagram account directly with Instagram Login.", true, true, true, true);
        Add("social", "Social", NewId(8), "threads", "Threads", "threads",
            "Coming soon — text and media posts on Threads.");
        Add("social", "Social", TwitterId, "twitter", "X (Twitter)", "twitter",
            "Coming soon — posts and engagement on X.");
        Add("social", "Social", LinkedInId, "linkedin", "LinkedIn", "linkedin",
            "Coming soon — professional company and personal posts.");
        Add("social", "Social", TikTokId, "tiktok", "TikTok", "tiktok",
            "Coming soon — short-form video publishing.");
        Add("social", "Social", YouTubeId, "youtube", "YouTube", "youtube",
            "Coming soon — video titles, descriptions, and uploads.");
        Add("social", "Social", NewId(9), "pinterest", "Pinterest", "pinterest",
            "Coming soon — pins and board publishing.");
        Add("social", "Social", NewId(10), "reddit", "Reddit", "reddit",
            "Coming soon — community posts and comments.");
        Add("social", "Social", NewId(11), "snapchat", "Snapchat", "snapchat",
            "Coming soon — Snapchat Spotlight and ads.");

        // Communication
        Add("communication", "Communication", WhatsAppId, "whatsapp", "WhatsApp Business", "whatsapp",
            "Send and receive WhatsApp Business Cloud API messages.", false, true, false, true);
        Add("communication", "Communication", NewId(12), "outlook", "Outlook", "outlook",
            "Coming soon — Outlook mail sync and replies.");
        Add("communication", "Communication", NewId(13), "gmail", "Gmail", "gmail",
            "Coming soon — Gmail inbox sync and send.");
        Add("communication", "Communication", NewId(14), "microsoft365", "Microsoft 365", "microsoft365",
            "Coming soon — Microsoft 365 productivity suite.");
        Add("communication", "Communication", NewId(15), "exchange", "Exchange", "exchange",
            "Coming soon — Exchange mailbox integration.");
        Add("communication", "Communication", NewId(16), "telegram", "Telegram", "telegram",
            "Coming soon — Telegram bots and chats.");
        Add("communication", "Communication", NewId(17), "discord", "Discord", "discord",
            "Coming soon — Discord servers and channels.");
        Add("communication", "Communication", NewId(18), "slack", "Slack", "slack",
            "Coming soon — Slack workspace messaging.");
        Add("communication", "Communication", NewId(19), "teams", "Microsoft Teams", "teams",
            "Coming soon — Teams channels and chats.");

        // Commerce
        Add("commerce", "Commerce", NewId(20), "shopify", "Shopify", "shopify",
            "Coming soon — store catalog and order sync.");
        Add("commerce", "Commerce", NewId(21), "woocommerce", "WooCommerce", "woocommerce",
            "Coming soon — WordPress store integration.");
        Add("commerce", "Commerce", NewId(22), "tiktokshop", "TikTok Shop", "tiktokshop",
            "Coming soon — TikTok Shop catalog and orders.");
        Add("commerce", "Commerce", NewId(23), "amazon", "Amazon", "amazon",
            "Coming soon — Amazon Seller Central.");
        Add("commerce", "Commerce", NewId(24), "etsy", "Etsy", "etsy",
            "Coming soon — Etsy shop listings and orders.");
        Add("commerce", "Commerce", NewId(25), "ebay", "eBay", "ebay",
            "Coming soon — eBay listings and orders.");

        // CRM
        Add("crm", "CRM", NewId(26), "salesforce", "Salesforce", "salesforce",
            "Coming soon — Salesforce CRM sync.");
        Add("crm", "CRM", NewId(27), "hubspot", "HubSpot", "hubspot",
            "Coming soon — HubSpot contacts and pipelines.");
        Add("crm", "CRM", NewId(28), "zoho", "Zoho", "zoho",
            "Coming soon — Zoho CRM records.");
        Add("crm", "CRM", NewId(29), "dynamics365", "Dynamics 365", "dynamics365",
            "Coming soon — Microsoft Dynamics 365 CRM.");
        Add("crm", "CRM", NewId(30), "pipedrive", "Pipedrive", "pipedrive",
            "Coming soon — Pipedrive deals and contacts.");

        // Calendar
        Add("calendar", "Calendar", NewId(31), "googlecalendar", "Google Calendar", "googlecalendar",
            "Coming soon — Google Calendar events.");
        Add("calendar", "Calendar", NewId(32), "outlookcalendar", "Outlook Calendar", "outlookcalendar",
            "Coming soon — Outlook calendar sync.");
        Add("calendar", "Calendar", NewId(33), "applecalendar", "Apple Calendar", "applecalendar",
            "Coming soon — Apple Calendar / CalDAV.");

        // Storage
        Add("storage", "Storage", NewId(34), "onedrive", "OneDrive", "onedrive",
            "Coming soon — OneDrive file storage.");
        Add("storage", "Storage", NewId(35), "googledrive", "Google Drive", "googledrive",
            "Coming soon — Google Drive files.");
        Add("storage", "Storage", NewId(36), "dropbox", "Dropbox", "dropbox",
            "Coming soon — Dropbox file storage.");
        Add("storage", "Storage", NewId(37), "sharepoint", "SharePoint", "sharepoint",
            "Coming soon — SharePoint document libraries.");

        // Payment
        Add("payment", "Payment", NewId(38), "stripe", "Stripe", "stripe",
            "Coming soon — Stripe payments and billing.");
        Add("payment", "Payment", NewId(39), "paypal", "PayPal", "paypal",
            "Coming soon — PayPal checkout and payouts.");
        Add("payment", "Payment", NewId(40), "square", "Square", "square",
            "Coming soon — Square payments.");

        // AI
        Add("ai", "AI", NewId(41), "openai", "OpenAI", "openai",
            "Coming soon — GPT models for content and replies.");
        Add("ai", "AI", NewId(42), "azureopenai", "Azure OpenAI", "azureopenai",
            "Coming soon — Azure-hosted OpenAI models.");
        Add("ai", "AI", NewId(43), "claude", "Claude", "claude",
            "Coming soon — Anthropic Claude assistants.");
        Add("ai", "AI", NewId(44), "gemini", "Gemini", "gemini",
            "Coming soon — Google Gemini models.");

        return list;
    }

    /// <summary>Stable platform row id for a catalog entry in a given process menu.</summary>
    public static Guid IdForMenu(Guid integrationId, string menuType)
    {
        var normalized = MenuTypes.Normalize(menuType);
        if (string.Equals(normalized, MenuTypes.Integration, StringComparison.OrdinalIgnoreCase))
            return integrationId;

        var bytes = integrationId.ToByteArray();
        if (string.Equals(normalized, MenuTypes.AppConnection, StringComparison.OrdinalIgnoreCase))
        {
            bytes[0] ^= 0xAC;
            bytes[1] ^= 0x01;
            return new Guid(bytes);
        }

        bytes[0] ^= 0xDA;
        bytes[1] ^= 0x03;
        return new Guid(bytes);
    }

    public static string DefaultAuthUrl(string platformCode, string graphVersion)
    {
        var code = platformCode.ToLowerInvariant();
        return code == "instagram_login"
            ? "https://www.instagram.com/oauth/authorize"
            : $"https://www.facebook.com/{graphVersion}/dialog/oauth";
    }

    public static string DefaultBaseUrl(string platformCode)
    {
        var code = platformCode.ToLowerInvariant();
        return code == "instagram_login"
            ? "https://graph.instagram.com"
            : "https://graph.facebook.com";
    }

    private static Guid NewId(int n) =>
        Guid.Parse($"aaaaaaaa-bbbb-cccc-dddd-{n:D12}");
}
