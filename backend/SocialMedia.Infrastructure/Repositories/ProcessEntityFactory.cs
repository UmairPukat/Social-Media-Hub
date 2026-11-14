using SocialMedia.Application.Catalog;
using SocialMedia.Domain.Modules.AppConnections.Entities;
using SocialMedia.Domain.Modules.Common.Entities;
using SocialMedia.Domain.Modules.DeveloperApps.Entities;
using SocialMedia.Domain.Modules.Integrations.Entities;
using SocialMedia.Domain.Modules.SocialMediaAccounts.Entities;
using SocialMedia.Domain.Modules.AccountConnections.Entities;
using SocialMedia.Domain.Modules.AccountManagement.Entities;
using SocialMedia.Domain.Modules.AccountHub.Entities;

namespace SocialMedia.Infrastructure.Repositories;

internal static class ProcessEntityFactory
{
    public static PlatformEntityBase NewPlatform(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionPlatform(),
        MenuTypes.DeveloperApp => new DeveloperAppPlatform(),
        MenuTypes.AccountConnection => new AccountConnectionPlatform(),
        MenuTypes.AccountManagement => new AccountManagementPlatform(),
        MenuTypes.AccountHub => new AccountHubPlatform(),
        MenuTypes.SocialMediaAccount => new SocialMediaAccountPlatform(),
        _ => new IntegrationPlatform()
    };

    public static SocialAccountEntityBase NewSocialAccount(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionSocialAccount(),
        MenuTypes.DeveloperApp => new DeveloperAppSocialAccount(),
        MenuTypes.AccountConnection => new AccountConnectionSocialAccount(),
        MenuTypes.AccountManagement => new AccountManagementSocialAccount(),
        MenuTypes.AccountHub => new AccountHubSocialAccount(),
        MenuTypes.SocialMediaAccount => new SocialMediaAccountSocialAccount(),
        _ => new IntegrationSocialAccount()
    };

    public static SocialAuthEntityBase NewSocialAuth(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionSocialAuth(),
        MenuTypes.DeveloperApp => new DeveloperAppSocialAuth(),
        MenuTypes.AccountConnection => new AccountConnectionSocialAuth(),
        MenuTypes.AccountManagement => new AccountManagementSocialAuth(),
        MenuTypes.AccountHub => new AccountHubSocialAuth(),
        MenuTypes.SocialMediaAccount => new SocialMediaAccountSocialAuth(),
        _ => new IntegrationSocialAuth()
    };

    public static SocialProfileEntityBase NewSocialProfile(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionSocialProfile(),
        MenuTypes.DeveloperApp => new DeveloperAppSocialProfile(),
        MenuTypes.AccountConnection => new AccountConnectionSocialProfile(),
        MenuTypes.AccountManagement => new AccountManagementSocialProfile(),
        MenuTypes.AccountHub => new AccountHubSocialProfile(),
        MenuTypes.SocialMediaAccount => new SocialMediaAccountSocialProfile(),
        _ => new IntegrationSocialProfile()
    };

    public static PostEntityBase NewPost(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionPost(),
        MenuTypes.DeveloperApp => new DeveloperAppPost(),
        MenuTypes.AccountConnection => new AccountConnectionPost(),
        MenuTypes.AccountManagement => new AccountManagementPost(),
        MenuTypes.AccountHub => new AccountHubPost(),
        MenuTypes.SocialMediaAccount => new SocialMediaAccountPost(),
        _ => new IntegrationPost()
    };

    public static CommentEntityBase NewComment(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionComment(),
        MenuTypes.DeveloperApp => new DeveloperAppComment(),
        MenuTypes.AccountConnection => new AccountConnectionComment(),
        MenuTypes.AccountManagement => new AccountManagementComment(),
        MenuTypes.AccountHub => new AccountHubComment(),
        MenuTypes.SocialMediaAccount => new SocialMediaAccountComment(),
        _ => new IntegrationComment()
    };

    public static ConversationEntityBase NewConversation(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionConversation(),
        MenuTypes.DeveloperApp => new DeveloperAppConversation(),
        MenuTypes.AccountConnection => new AccountConnectionConversation(),
        MenuTypes.AccountManagement => new AccountManagementConversation(),
        MenuTypes.AccountHub => new AccountHubConversation(),
        MenuTypes.SocialMediaAccount => new SocialMediaAccountConversation(),
        _ => new IntegrationConversation()
    };

    public static MessageEntityBase NewMessage(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionMessage(),
        MenuTypes.DeveloperApp => new DeveloperAppMessage(),
        MenuTypes.AccountConnection => new AccountConnectionMessage(),
        MenuTypes.AccountManagement => new AccountManagementMessage(),
        MenuTypes.AccountHub => new AccountHubMessage(),
        MenuTypes.SocialMediaAccount => new SocialMediaAccountMessage(),
        _ => new IntegrationMessage()
    };

    public static WebhookEventEntityBase NewWebhookEvent(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionWebhookEvent(),
        MenuTypes.DeveloperApp => new DeveloperAppWebhookEvent(),
        MenuTypes.AccountConnection => new AccountConnectionWebhookEvent(),
        MenuTypes.AccountManagement => new AccountManagementWebhookEvent(),
        MenuTypes.AccountHub => new AccountHubWebhookEvent(),
        MenuTypes.SocialMediaAccount => new SocialMediaAccountWebhookEvent(),
        _ => new IntegrationWebhookEvent()
    };

    public static WebhookLogEntityBase NewWebhookLog(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionWebhookLog(),
        MenuTypes.DeveloperApp => new DeveloperAppWebhookLog(),
        MenuTypes.AccountConnection => new AccountConnectionWebhookLog(),
        MenuTypes.AccountManagement => new AccountManagementWebhookLog(),
        MenuTypes.AccountHub => new AccountHubWebhookLog(),
        MenuTypes.SocialMediaAccount => new SocialMediaAccountWebhookLog(),
        _ => new IntegrationWebhookLog()
    };

    public static MediaEntityBase NewMedia(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionMedia(),
        MenuTypes.DeveloperApp => new DeveloperAppMedia(),
        MenuTypes.AccountConnection => new AccountConnectionMedia(),
        MenuTypes.AccountManagement => new AccountManagementMedia(),
        MenuTypes.AccountHub => new AccountHubMedia(),
        MenuTypes.SocialMediaAccount => new SocialMediaAccountMedia(),
        _ => new IntegrationMedia()
    };

    public static SyncJobEntityBase NewSyncJob(string menuType) => Normalize(menuType) switch
    {
        MenuTypes.AppConnection => new AppConnectionSyncJob(),
        MenuTypes.DeveloperApp => new DeveloperAppSyncJob(),
        MenuTypes.AccountConnection => new AccountConnectionSyncJob(),
        MenuTypes.AccountManagement => new AccountManagementSyncJob(),
        MenuTypes.AccountHub => new AccountHubSyncJob(),
        MenuTypes.SocialMediaAccount => new SocialMediaAccountSyncJob(),
        _ => new IntegrationSyncJob()
    };

    private static string Normalize(string menuType) => MenuTypes.Normalize(menuType);
}
