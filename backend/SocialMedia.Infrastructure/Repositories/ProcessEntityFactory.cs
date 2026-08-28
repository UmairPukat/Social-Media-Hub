using SocialMedia.Application.Catalog;
using SocialMedia.Domain.Modules.AppConnections.Entities;
using SocialMedia.Domain.Modules.Common.Entities;
using SocialMedia.Domain.Modules.DeveloperApps.Entities;
using SocialMedia.Domain.Modules.Integrations.Entities;

namespace SocialMedia.Infrastructure.Repositories;

internal static class ProcessEntityFactory
{
    public static PlatformEntityBase NewPlatform(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionPlatform(),
        MenuTypes.DeveloperApp => new DeveloperAppPlatform(),
        _ => new IntegrationPlatform()
    };

    public static SocialAccountEntityBase NewSocialAccount(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionSocialAccount(),
        MenuTypes.DeveloperApp => new DeveloperAppSocialAccount(),
        _ => new IntegrationSocialAccount()
    };

    public static SocialAuthEntityBase NewSocialAuth(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionSocialAuth(),
        MenuTypes.DeveloperApp => new DeveloperAppSocialAuth(),
        _ => new IntegrationSocialAuth()
    };

    public static SocialProfileEntityBase NewSocialProfile(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionSocialProfile(),
        MenuTypes.DeveloperApp => new DeveloperAppSocialProfile(),
        _ => new IntegrationSocialProfile()
    };

    public static PostEntityBase NewPost(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionPost(),
        MenuTypes.DeveloperApp => new DeveloperAppPost(),
        _ => new IntegrationPost()
    };

    public static CommentEntityBase NewComment(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionComment(),
        MenuTypes.DeveloperApp => new DeveloperAppComment(),
        _ => new IntegrationComment()
    };

    public static ConversationEntityBase NewConversation(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionConversation(),
        MenuTypes.DeveloperApp => new DeveloperAppConversation(),
        _ => new IntegrationConversation()
    };

    public static MessageEntityBase NewMessage(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionMessage(),
        MenuTypes.DeveloperApp => new DeveloperAppMessage(),
        _ => new IntegrationMessage()
    };

    public static WebhookEventEntityBase NewWebhookEvent(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionWebhookEvent(),
        MenuTypes.DeveloperApp => new DeveloperAppWebhookEvent(),
        _ => new IntegrationWebhookEvent()
    };

    public static WebhookLogEntityBase NewWebhookLog(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionWebhookLog(),
        MenuTypes.DeveloperApp => new DeveloperAppWebhookLog(),
        _ => new IntegrationWebhookLog()
    };

    public static MediaEntityBase NewMedia(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionMedia(),
        MenuTypes.DeveloperApp => new DeveloperAppMedia(),
        _ => new IntegrationMedia()
    };

    public static SyncJobEntityBase NewSyncJob(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionSyncJob(),
        MenuTypes.DeveloperApp => new DeveloperAppSyncJob(),
        _ => new IntegrationSyncJob()
    };

    private static string Normalize(string menuType) => MenuTypes.Normalize(menuType);
}
