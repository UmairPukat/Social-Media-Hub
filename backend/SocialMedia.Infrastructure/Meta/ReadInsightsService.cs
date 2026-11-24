using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;
using SocialMedia.Application.Settings;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Infrastructure.Meta;

public class ReadInsightsService : IReadInsightsService
{
    private readonly MetaGraphClient _graph;
    private readonly IProcessDataStoreFactory _processData;
    private readonly IFacebookService _facebook;
    private readonly MetaSettings _settings;

    public ReadInsightsService(
        MetaGraphClient graph,
        IProcessDataStoreFactory processData,
        IFacebookService facebook,
        IOptions<MetaSettings> settings)
    {
        _graph = graph;
        _processData = processData;
        _facebook = facebook;
        _settings = settings.Value;
    }

    public async Task<ApiResponse<ReadInsightsDto>> GetAsync(
        Guid userId,
        string menuType,
        int days,
        string? platform,
        CancellationToken cancellationToken = default)
    {
        if (!MenuTypes.IsKnown(menuType))
            return ApiResponse<ReadInsightsDto>.Fail("Unknown process menu.");

        days = days is 7 or 90 ? days : 28;
        var menu = MenuTypes.Normalize(menuType);
        var store = _processData.ForMenu(menu);
        var facebook = await LoadAccountAsync(store, userId, "facebook", cancellationToken);
        var instagram = await LoadAccountAsync(store, userId, "instagram", cancellationToken)
            ?? await LoadAccountAsync(store, userId, "instagram_login", cancellationToken);

        var available = new List<string>();
        if (facebook is not null) available.Add("facebook");
        if (instagram is not null) available.Add("instagram");
        if (available.Count == 0)
            return ApiResponse<ReadInsightsDto>.Fail("Connect a Facebook Page or Instagram account in this module to read insights.");

        var requested = (platform ?? string.Empty).Trim().ToLowerInvariant();
        var selected = available.Contains(requested)
            ? requested
            : available[0];

        var until = DateTime.UtcNow.Date.AddDays(1);
        var since = until.AddDays(-days);
        try
        {
            var report = selected == "instagram"
                ? await LoadInstagramAsync(instagram!, since, until, days, cancellationToken)
                : await LoadFacebookAsync(facebook!, since, until, days, cancellationToken);
            report.Days = days;
            report.AvailablePlatforms = available;
            return ApiResponse<ReadInsightsDto>.Ok(report, "Insights loaded.");
        }
        catch (Exception ex)
        {
            return ApiResponse<ReadInsightsDto>.Fail(ex.Message);
        }
    }

    private async Task<ReadInsightsDto> LoadFacebookAsync(
        ConnectedAccount account,
        DateTime since,
        DateTime until,
        int days,
        CancellationToken cancellationToken)
    {
        var (pageId, pageToken) = await MetaPagePublishHelper.ResolveFacebookPublishCredentialsAsync(
            _facebook, account.Account, account.Profile, account.Auth, cancellationToken);
        var version = VersionOrDefault(_settings.Facebook.GraphApiVersion);
        var notes = new List<string>();

        var pageViews = await FirstSeriesAsync(false, version, pageId, pageToken, "day", since, until, cancellationToken, "page_views_total", "page_views");
        var reach = await FirstSeriesAsync(false, version, pageId, pageToken, "day", since, until, cancellationToken, "page_impressions_unique", "page_posts_impressions_unique", "page_total_media_view_unique");
        var impressions = await FirstSeriesAsync(false, version, pageId, pageToken, "day", since, until, cancellationToken, "page_media_view", "page_posts_impressions", "page_impressions");
        var engagement = await FirstSeriesAsync(false, version, pageId, pageToken, "day", since, until, cancellationToken, "page_post_engagements");
        var followers = await FirstSeriesAsync(false, version, pageId, pageToken, "day", since, until, cancellationToken, "page_follows", "page_fans");
        if (followers is null && days > 30)
            followers = await FirstSeriesAsync(false, version, pageId, pageToken, "day", until.AddDays(-30), until, cancellationToken, "page_follows", "page_fans");

        if (pageViews is null)
            notes.Add("Page views are not available for this Facebook Page.");

        List<ReadInsightPostDto> posts;
        try
        {
            posts = await LoadFacebookPostsAsync(version, pageId, pageToken, cancellationToken);
        }
        catch (Exception ex)
        {
            posts = new List<ReadInsightPostDto>();
            notes.Add("Post performance could not be loaded: " + Trim(ex.Message));
        }
        return BuildReport("facebook", account.Profile.Name ?? "Facebook Page", pageId, pageViews, reach, impressions, engagement, followers, posts, notes);
    }

    private async Task<ReadInsightsDto> LoadInstagramAsync(
        ConnectedAccount account,
        DateTime since,
        DateTime until,
        int days,
        CancellationToken cancellationToken)
    {
        var igId = account.Profile.ExternalProfileId;
        if (string.IsNullOrWhiteSpace(igId))
            throw new InvalidOperationException("The connected Instagram account has no id. Reconnect Instagram and select the account again.");

        var token = FirstToken(
            ReadMeta(account.Profile.MetadataJson, "pageAccessToken"),
            account.Auth.AccessToken,
            account.Auth.RefreshToken);
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("No Instagram access token is stored. Reconnect Instagram.");

        var instagramLogin = string.Equals(account.PlatformCode, "instagram_login", StringComparison.OrdinalIgnoreCase);
        var version = VersionOrDefault(instagramLogin
            ? _settings.InstagramLogin.GraphApiVersion
            : _settings.Facebook.GraphApiVersion);

        var pageViews = await FirstSeriesAsync(instagramLogin, version, igId, token, "day", since, until, cancellationToken, "profile_views");
        var reach = await FirstSeriesAsync(instagramLogin, version, igId, token, "day", since, until, cancellationToken, "reach");
        var impressions = await FirstSeriesAsync(instagramLogin, version, igId, token, "day", since, until, cancellationToken, "views", "impressions");
        var engagement = await FirstSeriesAsync(instagramLogin, version, igId, token, "day", since, until, cancellationToken, "total_interactions", "accounts_engaged");
        var followerSince = days > 30 ? until.AddDays(-30) : since;
        var followers = await FirstSeriesAsync(instagramLogin, version, igId, token, "day", followerSince, until, cancellationToken, "follower_count");

        var notes = new List<string>();
        if (pageViews is null)
            notes.Add("Profile views are not available for this Instagram account.");
        if (days > 30 && followers is not null)
            notes.Add("Instagram follower history is limited to the last 30 days.");

        List<ReadInsightPostDto> posts;
        try
        {
            posts = await LoadInstagramPostsAsync(instagramLogin, version, igId, token, cancellationToken);
        }
        catch (Exception ex)
        {
            posts = new List<ReadInsightPostDto>();
            notes.Add("Post performance could not be loaded: " + Trim(ex.Message));
        }
        var name = account.Profile.Name ?? account.Profile.Username ?? "Instagram";
        return BuildReport("instagram", name, igId, pageViews, reach, impressions, engagement, followers, posts, notes);
    }

    private static ReadInsightsDto BuildReport(
        string platform,
        string accountName,
        string accountId,
        List<SeriesPoint>? pageViews,
        List<SeriesPoint>? reach,
        List<SeriesPoint>? impressions,
        List<SeriesPoint>? engagement,
        List<SeriesPoint>? followers,
        IReadOnlyList<ReadInsightPostDto> posts,
        IReadOnlyList<string> notes)
    {
        var dates = new SortedSet<string>(StringComparer.Ordinal);
        CollectDates(dates, pageViews);
        CollectDates(dates, reach);
        CollectDates(dates, impressions);
        CollectDates(dates, engagement);
        CollectDates(dates, followers);

        var trend = dates.Select(date => new ReadInsightPointDto
        {
            Date = date,
            PageViews = ValueOn(pageViews, date),
            Reach = ValueOn(reach, date),
            Impressions = ValueOn(impressions, date),
            Engagement = ValueOn(engagement, date),
            Followers = ValueOn(followers, date)
        }).ToList();

        return new ReadInsightsDto
        {
            Platform = platform,
            AccountName = accountName,
            AccountId = accountId,
            PageViewsSupported = pageViews is not null,
            PageViews = pageViews is null ? null : pageViews.Sum(point => point.Value),
            ReachSupported = reach is not null,
            Reach = reach is null ? null : reach.Sum(point => point.Value),
            ImpressionsSupported = impressions is not null,
            Impressions = impressions is null ? null : impressions.Sum(point => point.Value),
            EngagementSupported = engagement is not null,
            Engagement = engagement is null ? null : engagement.Sum(point => point.Value),
            FollowersSupported = followers is not null,
            Followers = followers is { Count: > 0 } ? followers[^1].Value : null,
            FollowerGrowth = Growth(followers),
            Trend = trend,
            Posts = posts,
            Notes = notes
        };
    }

    private async Task<List<ReadInsightPostDto>> LoadFacebookPostsAsync(
        string version,
        string pageId,
        string token,
        CancellationToken cancellationToken)
    {
        using var doc = await _graph.GetAsync(
            version,
            $"{pageId}/published_posts",
            token,
            cancellationToken,
            ("fields", "id,message,created_time,full_picture,shares,comments.summary(true).limit(0),reactions.summary(true).limit(0)"),
            ("limit", "8"));

        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return new List<ReadInsightPostDto>();

        var posts = new List<ReadInsightPostDto>();
        foreach (var row in data.EnumerateArray())
        {
            var id = MetaGraphResponseHelper.ReadString(row, "id");
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var reactions = SummaryCount(row, "reactions");
            var comments = SummaryCount(row, "comments");
            var shares = row.TryGetProperty("shares", out var shareNode) ? ReadLong(shareNode, "count") : 0;
            long? impressions = null;
            long? reach = null;
            var insight = await FirstSeriesAsync(false, version, id, token, "lifetime", null, null, cancellationToken, "post_media_view", "post_impressions");
            if (insight is { Count: > 0 })
                impressions = insight.Sum(point => point.Value);
            var reachSeries = await FirstSeriesAsync(false, version, id, token, "lifetime", null, null, cancellationToken, "post_impressions_unique");
            if (reachSeries is { Count: > 0 })
                reach = reachSeries.Sum(point => point.Value);

            posts.Add(new ReadInsightPostDto
            {
                Id = id,
                Message = Trim(MetaGraphResponseHelper.ReadString(row, "message")),
                CreatedTime = MetaGraphResponseHelper.ReadString(row, "created_time"),
                PictureUrl = MetaGraphResponseHelper.ReadString(row, "full_picture"),
                Impressions = impressions,
                Reach = reach,
                Reactions = reactions,
                Comments = comments,
                Shares = shares,
                Engagement = reactions + comments + shares
            });
        }

        return posts;
    }

    private async Task<List<ReadInsightPostDto>> LoadInstagramPostsAsync(
        bool instagramHost,
        string version,
        string igId,
        string token,
        CancellationToken cancellationToken)
    {
        using var doc = await GetAsync(
            instagramHost,
            version,
            $"{igId}/media",
            token,
            cancellationToken,
            ("fields", "id,caption,timestamp,media_url,thumbnail_url,like_count,comments_count"),
            ("limit", "8"));

        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return new List<ReadInsightPostDto>();

        var posts = new List<ReadInsightPostDto>();
        foreach (var row in data.EnumerateArray())
        {
            var id = MetaGraphResponseHelper.ReadString(row, "id");
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var likes = ReadLong(row, "like_count");
            var comments = ReadLong(row, "comments_count");
            long? impressions = null;
            long? reach = null;
            var views = await FirstSeriesAsync(instagramHost, version, id, token, "lifetime", null, null, cancellationToken, "views", "impressions");
            if (views is { Count: > 0 })
                impressions = views.Sum(point => point.Value);
            var reachSeries = await FirstSeriesAsync(instagramHost, version, id, token, "lifetime", null, null, cancellationToken, "reach");
            if (reachSeries is { Count: > 0 })
                reach = reachSeries.Sum(point => point.Value);

            posts.Add(new ReadInsightPostDto
            {
                Id = id,
                Message = Trim(MetaGraphResponseHelper.ReadString(row, "caption")),
                CreatedTime = MetaGraphResponseHelper.ReadString(row, "timestamp"),
                PictureUrl = MetaGraphResponseHelper.ReadString(row, "thumbnail_url")
                    ?? MetaGraphResponseHelper.ReadString(row, "media_url"),
                Impressions = impressions,
                Reach = reach,
                Reactions = likes,
                Comments = comments,
                Shares = 0,
                Engagement = likes + comments
            });
        }

        return posts;
    }

    private async Task<List<SeriesPoint>?> FirstSeriesAsync(
        bool instagramHost,
        string version,
        string objectId,
        string token,
        string period,
        DateTime? since,
        DateTime? until,
        CancellationToken cancellationToken,
        params string[] metrics)
    {
        foreach (var metric in metrics)
        {
            var series = await TryMetricAsync(instagramHost, version, objectId, token, metric, period, since, until, cancellationToken);
            if (series is not null)
                return series;
        }

        return null;
    }

    private async Task<List<SeriesPoint>?> TryMetricAsync(
        bool instagramHost,
        string version,
        string objectId,
        string token,
        string metric,
        string period,
        DateTime? since,
        DateTime? until,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = new List<(string Key, string Value)>
            {
                ("metric", metric),
                ("period", period)
            };
            if (since.HasValue && until.HasValue)
            {
                query.Add(("since", since.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                query.Add(("until", until.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            }

            using var doc = await GetAsync(instagramHost, version, $"{objectId}/insights", token, cancellationToken, query.ToArray());
            return ParseSeries(doc.RootElement);
        }
        catch
        {
            return null;
        }
    }

    private Task<JsonDocument> GetAsync(
        bool instagramHost,
        string version,
        string path,
        string token,
        CancellationToken cancellationToken,
        params (string Key, string Value)[] query)
        => instagramHost
            ? _graph.GetInstagramAsync(version, path, token, cancellationToken, query)
            : _graph.GetAsync(version, path, token, cancellationToken, query);

    private static List<SeriesPoint> ParseSeries(JsonElement root)
    {
        var points = new List<SeriesPoint>();
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return points;

        foreach (var metric in data.EnumerateArray())
        {
            if (!metric.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var row in values.EnumerateArray())
            {
                var end = MetaGraphResponseHelper.ReadString(row, "end_time");
                var date = string.IsNullOrWhiteSpace(end) ? "total" : end.Length >= 10 ? end[..10] : end;
                var value = row.TryGetProperty("value", out var raw) ? ReadMetricValue(raw) : 0;
                points.Add(new SeriesPoint(date, value));
            }
        }

        return points;
    }

    private static long ReadMetricValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
            return value.TryGetInt64(out var whole) ? whole : (long)value.GetDecimal();
        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var parsed))
            return parsed;
        if (value.ValueKind != JsonValueKind.Object)
            return 0;

        long total = 0;
        foreach (var property in value.EnumerateObject())
            total += ReadMetricValue(property.Value);
        return total;
    }

    private static long SummaryCount(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var edge))
            return 0;
        if (edge.TryGetProperty("summary", out var summary))
            return ReadLong(summary, "total_count");
        return ReadLong(edge, "count");
    }

    private static long ReadLong(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return 0;
        return ReadMetricValue(value);
    }

    private async Task<ConnectedAccount?> LoadAccountAsync(
        IProcessDataStore store,
        Guid userId,
        string platformCode,
        CancellationToken cancellationToken)
    {
        var platform = await store.GetPlatformByCodeAsync(platformCode, cancellationToken);
        if (platform is null)
            return null;

        var account = await store.GetSocialAccountByUserAndPlatformAsync(userId, platform.Id, cancellationToken);
        if (account is null || account.Status != SocialAccountStatus.Connected)
            return null;

        var auth = ProcessEntityNav.Auth(account) ?? await store.GetSocialAuthByAccountIdAsync(account.Id, cancellationToken);
        var profile = ProcessEntityNav.Profiles(account).FirstOrDefault();
        if (auth is null || profile is null)
            return null;

        return new ConnectedAccount(account, auth, profile, platformCode);
    }

    private static void CollectDates(SortedSet<string> dates, List<SeriesPoint>? series)
    {
        if (series is null)
            return;
        foreach (var point in series)
        {
            if (!string.IsNullOrWhiteSpace(point.Date) && point.Date != "total")
                dates.Add(point.Date);
        }
    }

    private static long? ValueOn(List<SeriesPoint>? series, string date)
        => series?.LastOrDefault(point => point.Date == date) is { } match ? match.Value : null;

    private static long? Growth(List<SeriesPoint>? series)
    {
        if (series is not { Count: >= 2 })
            return series is { Count: 1 } ? 0 : null;
        return series[^1].Value - series[0].Value;
    }

    private static string Trim(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Untitled post";
        var compact = text.Replace('\n', ' ').Trim();
        return compact.Length <= 160 ? compact : compact[..157] + "...";
    }

    private static string? FirstToken(params string?[] tokens)
        => tokens.FirstOrDefault(token => !string.IsNullOrWhiteSpace(token));

    private static string? ReadMeta(string? json, string property)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(property, out var value) ? value.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string VersionOrDefault(string? version)
        => string.IsNullOrWhiteSpace(version) ? "v21.0" : version.Trim();

    private sealed record SeriesPoint(string Date, long Value);

    private sealed record ConnectedAccount(
        SocialAccountEntityBase Account,
        SocialAuthEntityBase Auth,
        SocialProfileEntityBase Profile,
        string PlatformCode);
}
