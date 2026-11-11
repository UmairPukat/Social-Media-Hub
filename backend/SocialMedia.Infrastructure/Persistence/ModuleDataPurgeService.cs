using Microsoft.EntityFrameworkCore;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Modules.AccountConnections.Entities;
using SocialMedia.Domain.Modules.AppConnections.Entities;
using SocialMedia.Domain.Modules.Common.Entities;
using SocialMedia.Domain.Modules.DeveloperApps.Entities;
using SocialMedia.Domain.Modules.Integrations.Entities;
using SocialMedia.Domain.Modules.SocialMediaAccounts.Entities;

namespace SocialMedia.Infrastructure.Persistence;

public class ModuleDataPurgeService : IModuleDataPurgeService
{
    private readonly AppDbContext _db;

    public ModuleDataPurgeService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<ModulePurgeResultDto>> PurgeAsync(
        string menuType,
        CancellationToken cancellationToken = default)
    {
        var requested = (menuType ?? string.Empty).Trim().ToLowerInvariant();
        if (requested != "all" && !MenuTypes.IsKnown(requested))
            return ApiResponse<ModulePurgeResultDto>.Fail("Unknown module.");

        var menus = requested == "all"
            ? ProcessModules.AllMenuTypes
            : [MenuTypes.Normalize(requested)];

        try
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            var deleted = await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
                var count = 0;
                foreach (var menu in menus)
                    count += await PurgeMenuAsync(menu, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return count;
            });

            var label = requested == "all"
                ? "All modules"
                : ProcessModules.AllMenuTypes.Contains(MenuTypes.Normalize(requested))
                    ? LabelFor(MenuTypes.Normalize(requested))
                    : requested;

            return ApiResponse<ModulePurgeResultDto>.Ok(
                new ModulePurgeResultDto
                {
                    MenuType = requested == "all" ? "all" : MenuTypes.Normalize(requested),
                    Label = label,
                    DeletedRows = deleted
                },
                deleted == 0
                    ? $"{label} had no posts, comments, messages, accounts, or webhook records to delete."
                    : $"Permanently deleted {deleted} {label} record(s).");
        }
        catch (Exception ex)
        {
            return ApiResponse<ModulePurgeResultDto>.Fail(ex.Message);
        }
    }

    private async Task<int> PurgeMenuAsync(string menuType, CancellationToken cancellationToken)
    {
        return MenuTypes.Normalize(menuType) switch
        {
            MenuTypes.AppConnection => await PurgeAsync<
                AppConnectionMessageAttachment, AppConnectionMessage, AppConnectionConversation,
                AppConnectionComment, AppConnectionMedia, AppConnectionPost, AppConnectionSyncJob,
                AppConnectionWebhookLog, AppConnectionWebhookEvent,
                AppConnectionSocialAuth, AppConnectionSocialProfile, AppConnectionSocialAccount>(cancellationToken),
            MenuTypes.DeveloperApp => await PurgeAsync<
                DeveloperAppMessageAttachment, DeveloperAppMessage, DeveloperAppConversation,
                DeveloperAppComment, DeveloperAppMedia, DeveloperAppPost, DeveloperAppSyncJob,
                DeveloperAppWebhookLog, DeveloperAppWebhookEvent,
                DeveloperAppSocialAuth, DeveloperAppSocialProfile, DeveloperAppSocialAccount>(cancellationToken),
            MenuTypes.SocialMediaAccount => await PurgeAsync<
                SocialMediaAccountMessageAttachment, SocialMediaAccountMessage, SocialMediaAccountConversation,
                SocialMediaAccountComment, SocialMediaAccountMedia, SocialMediaAccountPost, SocialMediaAccountSyncJob,
                SocialMediaAccountWebhookLog, SocialMediaAccountWebhookEvent,
                SocialMediaAccountSocialAuth, SocialMediaAccountSocialProfile, SocialMediaAccountSocialAccount>(cancellationToken),
            MenuTypes.AccountConnection => await PurgeAsync<
                AccountConnectionMessageAttachment, AccountConnectionMessage, AccountConnectionConversation,
                AccountConnectionComment, AccountConnectionMedia, AccountConnectionPost, AccountConnectionSyncJob,
                AccountConnectionWebhookLog, AccountConnectionWebhookEvent,
                AccountConnectionSocialAuth, AccountConnectionSocialProfile, AccountConnectionSocialAccount>(cancellationToken),
            _ => await PurgeAsync<
                IntegrationMessageAttachment, IntegrationMessage, IntegrationConversation,
                IntegrationComment, IntegrationMedia, IntegrationPost, IntegrationSyncJob,
                IntegrationWebhookLog, IntegrationWebhookEvent,
                IntegrationSocialAuth, IntegrationSocialProfile, IntegrationSocialAccount>(cancellationToken)
        };
    }

    private async Task<int> PurgeAsync<TAttachment, TMessage, TConversation, TComment, TMedia, TPost, TSync, TLog, TEvent, TAuth, TProfile, TAccount>(
        CancellationToken cancellationToken)
        where TAttachment : MessageAttachmentEntityBase
        where TMessage : MessageEntityBase
        where TConversation : ConversationEntityBase
        where TComment : CommentEntityBase
        where TMedia : MediaEntityBase
        where TPost : PostEntityBase
        where TSync : SyncJobEntityBase
        where TLog : WebhookLogEntityBase
        where TEvent : WebhookEventEntityBase
        where TAuth : SocialAuthEntityBase
        where TProfile : SocialProfileEntityBase
        where TAccount : SocialAccountEntityBase
    {
        await _db.Set<TComment>().ExecuteUpdateAsync(
            setters => setters.SetProperty(comment => comment.ParentCommentId, (Guid?)null),
            cancellationToken);
        await _db.Set<TMessage>().ExecuteUpdateAsync(
            setters => setters.SetProperty(message => message.ReplyToMessageId, (Guid?)null),
            cancellationToken);

        var deleted = 0;
        deleted += await _db.Set<TAttachment>().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.Set<TMessage>().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.Set<TConversation>().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.Set<TComment>().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.Set<TMedia>().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.Set<TPost>().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.Set<TSync>().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.Set<TLog>().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.Set<TEvent>().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.Set<TAuth>().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.Set<TProfile>().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.Set<TAccount>().ExecuteDeleteAsync(cancellationToken);
        return deleted;
    }

    private static string LabelFor(string menuType) => menuType switch
    {
        MenuTypes.AppConnection => ProcessModules.AppConnections.Label,
        MenuTypes.DeveloperApp => ProcessModules.DeveloperApps.Label,
        MenuTypes.SocialMediaAccount => ProcessModules.SocialMediaAccounts.Label,
        MenuTypes.AccountConnection => ProcessModules.AccountConnections.Label,
        _ => ProcessModules.Integrations.Label
    };
}
