using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.AppConnections.Entities;
using SocialMedia.Domain.Modules.Common.Entities;
using SocialMedia.Domain.Modules.DeveloperApps.Entities;
using SocialMedia.Domain.Modules.Integrations.Entities;
using SocialMedia.Domain.Modules.SocialMediaAccounts.Entities;
using SocialMedia.Domain.Modules.AccountConnections.Entities;
using SocialMedia.Domain.Modules.AccountManagement.Entities;
using SocialMedia.Domain.Modules.AccountHub.Entities;

namespace SocialMedia.Application.Meta;

/// <summary>
/// Accesses navigation properties on module-scoped entities returned as base types.
/// </summary>
public static class ProcessEntityNav
{
    public static SocialAuthEntityBase? Auth(SocialAccountEntityBase account) => account switch
    {
        IntegrationSocialAccount i => i.Auth,
        AppConnectionSocialAccount a => a.Auth,
        DeveloperAppSocialAccount d => d.Auth,
        AccountConnectionSocialAccount d => d.Auth,
        AccountManagementSocialAccount d => d.Auth,
        AccountHubSocialAccount d => d.Auth,
        SocialMediaAccountSocialAccount d => d.Auth,
        _ => null
    };

    public static IReadOnlyList<SocialProfileEntityBase> Profiles(SocialAccountEntityBase account) => account switch
    {
        IntegrationSocialAccount i when i.Profiles is { } ip => ip.Cast<SocialProfileEntityBase>().ToList(),
        AppConnectionSocialAccount a when a.Profiles is { } ap => ap.Cast<SocialProfileEntityBase>().ToList(),
        DeveloperAppSocialAccount d when d.Profiles is { } dp => dp.Cast<SocialProfileEntityBase>().ToList(),
        AccountConnectionSocialAccount d when d.Profiles is { } dp => dp.Cast<SocialProfileEntityBase>().ToList(),
        AccountManagementSocialAccount d when d.Profiles is { } dp => dp.Cast<SocialProfileEntityBase>().ToList(),
        AccountHubSocialAccount d when d.Profiles is { } dp => dp.Cast<SocialProfileEntityBase>().ToList(),
        SocialMediaAccountSocialAccount d when d.Profiles is { } dp => dp.Cast<SocialProfileEntityBase>().ToList(),
        _ => Array.Empty<SocialProfileEntityBase>()
    };

    public static PlatformEntityBase? Platform(SocialAccountEntityBase account) => account switch
    {
        IntegrationSocialAccount i => i.Platform,
        AppConnectionSocialAccount a => a.Platform,
        DeveloperAppSocialAccount d => d.Platform,
        AccountConnectionSocialAccount d => d.Platform,
        AccountManagementSocialAccount d => d.Platform,
        AccountHubSocialAccount d => d.Platform,
        SocialMediaAccountSocialAccount d => d.Platform,
        _ => null
    };

    public static string? PlatformCode(SocialAccountEntityBase account) => Platform(account)?.Code;

    public static SocialProfileEntityBase? Profile(PostEntityBase post) => post switch
    {
        IntegrationPost i => i.SocialProfile,
        AppConnectionPost a => a.SocialProfile,
        DeveloperAppPost d => d.SocialProfile,
        AccountConnectionPost d => d.SocialProfile,
        AccountManagementPost d => d.SocialProfile,
        AccountHubPost d => d.SocialProfile,
        SocialMediaAccountPost d => d.SocialProfile,
        _ => null
    };

    public static PlatformEntityBase? Platform(PostEntityBase post) => post switch
    {
        IntegrationPost i => i.Platform,
        AppConnectionPost a => a.Platform,
        DeveloperAppPost d => d.Platform,
        AccountConnectionPost d => d.Platform,
        AccountManagementPost d => d.Platform,
        AccountHubPost d => d.Platform,
        SocialMediaAccountPost d => d.Platform,
        _ => null
    };

    public static MediaEntityBase? FirstMedia(PostEntityBase post) => post switch
    {
        IntegrationPost i => i.MediaItems?.FirstOrDefault(),
        AppConnectionPost a => a.MediaItems?.FirstOrDefault(),
        DeveloperAppPost d => d.MediaItems?.FirstOrDefault(),
        AccountConnectionPost d => d.MediaItems?.FirstOrDefault(),
        AccountManagementPost d => d.MediaItems?.FirstOrDefault(),
        AccountHubPost d => d.MediaItems?.FirstOrDefault(),
        SocialMediaAccountPost d => d.MediaItems?.FirstOrDefault(),
        _ => null
    };

    /// <summary>
    /// Inbox renders an image/poster. Prefer a still (thumbnail) over a video file URL.
    /// </summary>
    public static string? FirstMediaUrl(PostEntityBase post)
    {
        var media = FirstMedia(post);
        if (media is null)
            return null;

        if (media.MediaType == MediaType.Video || LooksLikeVideo(media.Url))
            return FirstNonEmpty(media.Thumbnail, LooksLikeImage(media.Url) ? media.Url : null);

        return FirstNonEmpty(media.Url, media.Thumbnail);
    }

    public static string? FirstVideoUrl(PostEntityBase post)
    {
        var media = FirstMedia(post);
        if (media is null)
            return null;

        return media.MediaType == MediaType.Video || LooksLikeVideo(media.Url)
            ? (LooksLikeVideo(media.Url) ? media.Url : null)
            : null;
    }

    public static bool HasDisplayableMedia(PostEntityBase post)
        => IsBrowserDisplayableUrl(FirstMediaUrl(post))
           || IsBrowserDisplayableUrl(FirstVideoUrl(post));

    /// <summary>
    /// Facebook <c>full_picture</c> URLs render in an img tag. Instagram Login <c>media_url</c>
    /// (cdninstagram) is hotlink-blocked, so Inbox must serve a local copy instead.
    /// </summary>
    public static bool IsBrowserDisplayableUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        if (url.Contains("/publish-cache/", StringComparison.OrdinalIgnoreCase))
            return true;
        return !LooksLikeInstagramCdn(url);
    }

    public static bool LooksLikeInstagramCdn(string? url)
        => !string.IsNullOrWhiteSpace(url)
           && (url.Contains("cdninstagram.com", StringComparison.OrdinalIgnoreCase)
               || url.Contains("instagram.f", StringComparison.OrdinalIgnoreCase)
               || url.Contains("/t51.", StringComparison.OrdinalIgnoreCase));

    public static int MediaCount(PostEntityBase post) => post switch
    {
        IntegrationPost i => i.MediaItems?.Count ?? 0,
        AppConnectionPost a => a.MediaItems?.Count ?? 0,
        DeveloperAppPost d => d.MediaItems?.Count ?? 0,
        AccountConnectionPost d => d.MediaItems?.Count ?? 0,
        AccountManagementPost d => d.MediaItems?.Count ?? 0,
        AccountHubPost d => d.MediaItems?.Count ?? 0,
        SocialMediaAccountPost d => d.MediaItems?.Count ?? 0,
        _ => 0
    };

    private static bool LooksLikeVideo(string? url)
        => !string.IsNullOrWhiteSpace(url)
           && (url.Contains(".mp4", StringComparison.OrdinalIgnoreCase)
               || url.Contains(".mov", StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeImage(string? url)
        => !string.IsNullOrWhiteSpace(url)
           && (url.Contains(".jpg", StringComparison.OrdinalIgnoreCase)
               || url.Contains(".jpeg", StringComparison.OrdinalIgnoreCase)
               || url.Contains(".png", StringComparison.OrdinalIgnoreCase)
               || url.Contains(".webp", StringComparison.OrdinalIgnoreCase)
               || url.Contains("/publish-cache/", StringComparison.OrdinalIgnoreCase));

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    public static void AttachMedia(PostEntityBase post, MediaEntityBase media)
    {
        switch (post)
        {
            case IntegrationPost i:
                (i.MediaItems ??= new List<IntegrationMedia>()).Add((IntegrationMedia)media);
                break;
            case AppConnectionPost a:
                (a.MediaItems ??= new List<AppConnectionMedia>()).Add((AppConnectionMedia)media);
                break;
            case DeveloperAppPost d:
                (d.MediaItems ??= new List<DeveloperAppMedia>()).Add((DeveloperAppMedia)media);
                break;
            case AccountConnectionPost d:
                (d.MediaItems ??= new List<AccountConnectionMedia>()).Add((AccountConnectionMedia)media);
                break;
            case AccountManagementPost d:
                (d.MediaItems ??= new List<AccountManagementMedia>()).Add((AccountManagementMedia)media);
                break;
            case AccountHubPost d:
                (d.MediaItems ??= new List<AccountHubMedia>()).Add((AccountHubMedia)media);
                break;
            case SocialMediaAccountPost d:
                (d.MediaItems ??= new List<SocialMediaAccountMedia>()).Add((SocialMediaAccountMedia)media);
                break;
        }
    }

    public static SocialProfileEntityBase? Profile(ConversationEntityBase conversation) => conversation switch
    {
        IntegrationConversation i => i.SocialProfile,
        AppConnectionConversation a => a.SocialProfile,
        DeveloperAppConversation d => d.SocialProfile,
        AccountConnectionConversation d => d.SocialProfile,
        AccountManagementConversation d => d.SocialProfile,
        AccountHubConversation d => d.SocialProfile,
        SocialMediaAccountConversation d => d.SocialProfile,
        _ => null
    };

    public static int UnreadCount(ConversationEntityBase conversation) => conversation.UnreadCount;

    public static string? CustomerName(ConversationEntityBase conversation) => conversation.CustomerName;

    public static MessageDirection Direction(MessageEntityBase message) => message.Direction;

    public static MessageEntityBase? ReplyTo(MessageEntityBase message, IReadOnlyDictionary<Guid, MessageEntityBase> byId)
        => message.ReplyToMessageId.HasValue && byId.TryGetValue(message.ReplyToMessageId.Value, out var quoted)
            ? quoted
            : null;
}
