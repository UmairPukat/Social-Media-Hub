using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Application.Interfaces;

/// <summary>
/// Module-scoped data access for one process menu (integration, app_connection, developer_app).
/// </summary>
public interface IProcessDataStore
{
    string MenuType { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // Platforms
    Task<IReadOnlyList<PlatformEntityBase>> GetActivePlatformsAsync(CancellationToken cancellationToken = default);
    Task<PlatformEntityBase?> GetPlatformByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PlatformEntityBase?> GetPlatformByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task AddPlatformAsync(PlatformEntityBase platform, CancellationToken cancellationToken = default);

    // Social accounts
    Task<IReadOnlyList<SocialAccountEntityBase>> GetSocialAccountsByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SocialAccountEntityBase?> GetSocialAccountByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SocialAccountEntityBase?> GetSocialAccountByUserAndPlatformAsync(Guid userId, Guid platformId, CancellationToken cancellationToken = default);
    Task<SocialAccountEntityBase?> GetSocialAccountByExternalIdAsync(string externalAccountId, CancellationToken cancellationToken = default);
    Task<SocialAccountEntityBase?> GetSocialAccountWithAuthAndProfilesAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddSocialAccountAsync(SocialAccountEntityBase account, CancellationToken cancellationToken = default);
    void UpdateSocialAccount(SocialAccountEntityBase account);
    void RemoveSocialAccount(SocialAccountEntityBase account);

    // Social auth
    Task<SocialAuthEntityBase?> GetSocialAuthByAccountIdAsync(Guid socialAccountId, CancellationToken cancellationToken = default);
    Task AddSocialAuthAsync(SocialAuthEntityBase auth, CancellationToken cancellationToken = default);
    void UpdateSocialAuth(SocialAuthEntityBase auth);

    // Social profiles
    Task<IReadOnlyList<SocialProfileEntityBase>> GetProfilesByAccountAsync(Guid socialAccountId, CancellationToken cancellationToken = default);
    Task<SocialProfileEntityBase?> GetProfileByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SocialProfileEntityBase?> GetProfileByExternalIdAsync(string externalProfileId, CancellationToken cancellationToken = default);
    Task AddSocialProfileAsync(SocialProfileEntityBase profile, CancellationToken cancellationToken = default);
    void UpdateSocialProfile(SocialProfileEntityBase profile);
    void RemoveSocialProfile(SocialProfileEntityBase profile);
    Task<IReadOnlyList<SocialProfileEntityBase>> FindProfilesByExternalIdAsync(string externalProfileId, CancellationToken cancellationToken = default);

    // Posts
    Task<IReadOnlyList<PostEntityBase>> GetPostsByUserProfilesAsync(Guid userId, Guid? platformId = null, CancellationToken cancellationToken = default);
    Task<PostEntityBase?> GetPostByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PostEntityBase?> GetPostByExternalIdAsync(Guid socialProfileId, string externalPostId, CancellationToken cancellationToken = default);
    Task AddPostAsync(PostEntityBase post, CancellationToken cancellationToken = default);
    void UpdatePost(PostEntityBase post);
    void RemovePost(PostEntityBase post);

    // Comments
    Task<IReadOnlyList<InboxCommentRow>> GetCommentsForInboxAsync(Guid userId, Guid? platformId, IReadOnlyList<Guid>? platformIds, CancellationToken cancellationToken = default);
    Task<CommentEntityBase?> GetCommentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CommentEntityBase?> GetCommentByExternalIdAsync(string externalCommentId, CancellationToken cancellationToken = default);
    Task AddCommentAsync(CommentEntityBase comment, CancellationToken cancellationToken = default);
    void UpdateComment(CommentEntityBase comment);

    // Conversations
    Task<IReadOnlyList<InboxMessageRow>> GetMessagesForInboxAsync(Guid userId, Guid? platformId, IReadOnlyList<Guid>? platformIds, CancellationToken cancellationToken = default);
    Task<ConversationEntityBase?> GetConversationByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ConversationEntityBase?> GetConversationByExternalIdAsync(Guid socialProfileId, string externalConversationId, CancellationToken cancellationToken = default);
    Task<ConversationEntityBase?> GetConversationByProfileAndCustomerAsync(Guid socialProfileId, string customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConversationEntityBase>> GetConversationsByProfileIdAsync(Guid profileId, CancellationToken cancellationToken = default);
    Task AddConversationAsync(ConversationEntityBase conversation, CancellationToken cancellationToken = default);
    void UpdateConversation(ConversationEntityBase conversation);

    // Messages
    Task<MessageEntityBase?> GetMessageByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MessageEntityBase?> GetMessageByExternalIdAsync(string externalMessageId, CancellationToken cancellationToken = default);
    Task AddMessageAsync(MessageEntityBase message, CancellationToken cancellationToken = default);
    void UpdateMessage(MessageEntityBase message);
    void RemoveMessage(MessageEntityBase message);

    // Webhooks
    Task AddWebhookEventAsync(WebhookEventEntityBase webhookEvent, CancellationToken cancellationToken = default);
    void UpdateWebhookEvent(WebhookEventEntityBase webhookEvent);
    Task AddWebhookLogAsync(WebhookLogEntityBase webhookLog, CancellationToken cancellationToken = default);

    // Sync jobs
    Task AddSyncJobAsync(SyncJobEntityBase syncJob, CancellationToken cancellationToken = default);

    // Factories
    PlatformEntityBase NewPlatform();
    SocialAccountEntityBase NewSocialAccount();
    SocialAuthEntityBase NewSocialAuth();
    SocialProfileEntityBase NewSocialProfile();
    PostEntityBase NewPost();
    CommentEntityBase NewComment();
    ConversationEntityBase NewConversation();
    MessageEntityBase NewMessage();
    WebhookEventEntityBase NewWebhookEvent();
    WebhookLogEntityBase NewWebhookLog();
    MediaEntityBase NewMedia();
    SyncJobEntityBase NewSyncJob();

    Task<IReadOnlyList<SocialAccountEntityBase>> FindConnectedSocialAccountsAsync(CancellationToken cancellationToken = default);
    Task<SocialProfileEntityBase?> PickBestProfileForAccountAsync(Guid accountId, CancellationToken cancellationToken = default);
}

public sealed record InboxCommentRow(
    CommentEntityBase Comment,
    PostEntityBase Post,
    SocialProfileEntityBase Profile,
    SocialAccountEntityBase Account,
    PlatformEntityBase Platform,
    int ReplyCount,
    string? PostImageUrl);

public sealed record InboxMessageRow(
    MessageEntityBase Message,
    ConversationEntityBase Conversation,
    SocialProfileEntityBase Profile,
    SocialAccountEntityBase Account,
    PlatformEntityBase Platform);

public interface IProcessDataStoreFactory
{
    IProcessDataStore ForMenu(string menuType);
    IReadOnlyList<IProcessDataStore> AllStores();
}
