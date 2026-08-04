namespace SocialMedia.Application.DTOs.Dashboard;

/// <summary>
/// High-level counts shown on the dashboard's summary widgets.
/// </summary>
public class DashboardSummaryDto
{
    public int ConnectedAccountsCount { get; set; }
    public int TotalPostsCount { get; set; }
    public int PublishedPostsCount { get; set; }
    public int FailedPostsCount { get; set; }
    public int ScheduledPostsCount { get; set; }
    public int UnreadInboxCount { get; set; }
    public int TotalCommentsCount { get; set; }
    public int TotalMessagesCount { get; set; }
}
