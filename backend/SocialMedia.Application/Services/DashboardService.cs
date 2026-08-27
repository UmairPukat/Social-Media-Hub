using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Dashboard;
using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Interfaces;

namespace SocialMedia.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;

    public DashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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
            var accounts = await _unitOfWork.SocialAccounts.GetByUserAsync(userId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(menuType))
                accounts = accounts.Where(a => a.MenuType == menuType).ToList();

            var posts = await _unitOfWork.Posts.GetByUserProfilesAsync(
                userId, menuType: menuType, cancellationToken: cancellationToken);

            var comments = await _unitOfWork.Comments.GetByUserAsync(userId, cancellationToken: cancellationToken);
            if (!string.IsNullOrWhiteSpace(menuType))
            {
                comments = comments
                    .Where(c => c.Post?.SocialProfile?.SocialAccount?.MenuType == menuType)
                    .ToList();
            }

            var messages = await _unitOfWork.Messages.GetByUserAsync(userId, cancellationToken: cancellationToken);
            if (!string.IsNullOrWhiteSpace(menuType))
            {
                messages = messages
                    .Where(m => m.Conversation?.SocialProfile?.SocialAccount?.MenuType == menuType)
                    .ToList();
            }

            return ApiResponse<DashboardSummaryDto>.Ok(new DashboardSummaryDto
            {
                ConnectedAccountsCount = accounts.Count(a => a.Status == SocialAccountStatus.Connected),
                TotalPostsCount = posts.Count,
                PublishedPostsCount = posts.Count(p => p.Status == ContentPostStatus.Published),
                FailedPostsCount = posts.Count(p => p.Status == ContentPostStatus.Failed),
                ScheduledPostsCount = posts.Count(p => p.Status == ContentPostStatus.Scheduled),
                UnreadInboxCount = messages.Count(m => m.Conversation?.UnreadCount > 0),
                TotalCommentsCount = comments.Count,
                TotalMessagesCount = messages.Count
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<DashboardSummaryDto>.Fail(ex.Message);
        }
    }
}
