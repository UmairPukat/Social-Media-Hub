using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.AppConnections.Entities;
using SocialMedia.Domain.Modules.Common.Entities;
using SocialMedia.Domain.Modules.DeveloperApps.Entities;
using SocialMedia.Domain.Modules.Integrations.Entities;

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
        _ => null
    };

    public static IReadOnlyList<SocialProfileEntityBase> Profiles(SocialAccountEntityBase account) => account switch
    {
        IntegrationSocialAccount i => i.Profiles.Cast<SocialProfileEntityBase>().ToList(),
        AppConnectionSocialAccount a => a.Profiles.Cast<SocialProfileEntityBase>().ToList(),
        DeveloperAppSocialAccount d => d.Profiles.Cast<SocialProfileEntityBase>().ToList(),
        _ => Array.Empty<SocialProfileEntityBase>()
    };

    public static PlatformEntityBase? Platform(SocialAccountEntityBase account) => account switch
    {
        IntegrationSocialAccount i => i.Platform,
        AppConnectionSocialAccount a => a.Platform,
        DeveloperAppSocialAccount d => d.Platform,
        _ => null
    };

    public static string? PlatformCode(SocialAccountEntityBase account) => Platform(account)?.Code;

    public static SocialProfileEntityBase? Profile(PostEntityBase post) => post switch
    {
        IntegrationPost i => i.SocialProfile,
        AppConnectionPost a => a.SocialProfile,
        DeveloperAppPost d => d.SocialProfile,
        _ => null
    };

    public static PlatformEntityBase? Platform(PostEntityBase post) => post switch
    {
        IntegrationPost i => i.Platform,
        AppConnectionPost a => a.Platform,
        DeveloperAppPost d => d.Platform,
        _ => null
    };

    public static string? FirstMediaUrl(PostEntityBase post) => post switch
    {
        IntegrationPost i => i.MediaItems.FirstOrDefault()?.Url,
        AppConnectionPost a => a.MediaItems.FirstOrDefault()?.Url,
        DeveloperAppPost d => d.MediaItems.FirstOrDefault()?.Url,
        _ => null
    };

    public static int MediaCount(PostEntityBase post) => post switch
    {
        IntegrationPost i => i.MediaItems.Count,
        AppConnectionPost a => a.MediaItems.Count,
        DeveloperAppPost d => d.MediaItems.Count,
        _ => 0
    };

    public static void AttachMedia(PostEntityBase post, MediaEntityBase media)
    {
        switch (post)
        {
            case IntegrationPost i:
                i.MediaItems.Add((IntegrationMedia)media);
                break;
            case AppConnectionPost a:
                a.MediaItems.Add((AppConnectionMedia)media);
                break;
            case DeveloperAppPost d:
                d.MediaItems.Add((DeveloperAppMedia)media);
                break;
        }
    }

    public static SocialProfileEntityBase? Profile(ConversationEntityBase conversation) => conversation switch
    {
        IntegrationConversation i => i.SocialProfile,
        AppConnectionConversation a => a.SocialProfile,
        DeveloperAppConversation d => d.SocialProfile,
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
