using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Dashboard;
using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IProcessDataStoreFactory _processData;

    public DashboardService(IProcessDataStoreFactory processData)
    {
        _processData = processData;
    }

    public Task<ApiResponse<DashboardSummaryDto>> GetSummaryAsync(Guid userId, CancellationToken cancellationToken = default)
        => GetSummaryForProcessAsync(userId, null, cancellationToken);

    public async Task<ApiResponse<DashboardSummaryDto>> GetSummaryForProcessAsync(
        Guid userId,
        string? menuType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stores = string.IsNullOrWhiteSpace(menuType)
                ? _processData.AllStores()
                : [_processData.ForMenu(menuType)];

            var connectedAccounts = 0;
            var posts = new List<Domain.Modules.Common.Entities.PostEntityBase>();
            var commentCount = 0;
            var messageCount = 0;
            var unreadInbox = 0;

            foreach (var store in stores)
            {
                var accounts = await store.GetSocialAccountsByUserAsync(userId, cancellationToken);
                connectedAccounts += accounts.Count(a => a.Status == SocialAccountStatus.Connected);

                var storePosts = await store.GetPostsByUserProfilesAsync(userId, cancellationToken: cancellationToken);
                posts.AddRange(storePosts);

                var comments = await store.GetCommentsForInboxAsync(userId, null, null, cancellationToken);
                commentCount += comments.Count;

                var messages = await store.GetMessagesForInboxAsync(userId, null, null, cancellationToken);
                messageCount += messages.Count;
                unreadInbox += messages.Count(m => Meta.ProcessEntityNav.UnreadCount(m.Conversation) > 0);
            }

            return ApiResponse<DashboardSummaryDto>.Ok(new DashboardSummaryDto
            {
                ConnectedAccountsCount = connectedAccounts,
                TotalPostsCount = posts.Count,
                PublishedPostsCount = posts.Count(p => p.Status == ContentPostStatus.Published),
                FailedPostsCount = posts.Count(p => p.Status == ContentPostStatus.Failed),
                ScheduledPostsCount = posts.Count(p => p.Status == ContentPostStatus.Scheduled),
                UnreadInboxCount = unreadInbox,
                TotalCommentsCount = commentCount,
                TotalMessagesCount = messageCount
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<DashboardSummaryDto>.Fail(ex.Message);
        }
    }
}
