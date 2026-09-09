using System.Text.Json;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;

namespace SocialMedia.Infrastructure.Meta;

public class MetaInsightsService : IMetaInsightsService
{
    private const string InsightFields =
        "impressions,reach,clicks,spend,ctr,cpc,cpm,actions,date_start,date_stop";

    private readonly IMetaGraphApiClient _graph;

    public MetaInsightsService(IMetaGraphApiClient graph)
    {
        _graph = graph;
    }

    public Task<Application.DTOs.Common.ApiResponse<MetaInsightsSummaryDto>> GetInsightsAsync(
        Guid userId,
        string menuType,
        MetaInsightsQuery query,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(query.ObjectId))
                throw new MetaGraphApiException("Object id is required.");

            var queryParams = new List<(string, string)>
            {
                ("fields", InsightFields),
                ("time_increment", "1")
            };

            if (!string.IsNullOrWhiteSpace(query.Since) && !string.IsNullOrWhiteSpace(query.Until))
            {
                queryParams.Add(("time_range", JsonSerializer.Serialize(new
                {
                    since = query.Since,
                    until = query.Until
                })));
            }
            else
            {
                queryParams.Add(("date_preset", string.IsNullOrWhiteSpace(query.DatePreset) ? "last_7d" : query.DatePreset));
            }

            using var doc = await _graph.GetAsync(
                userId,
                menuType,
                $"{query.ObjectId.Trim()}/insights",
                cancellationToken,
                queryParams.ToArray());

            var rows = ReadInsightRows(doc.RootElement);
            return BuildSummary(query.ObjectId.Trim(), query.DatePreset, rows);
        }, "Insights loaded.");

    private static List<MetaInsightRowDto> ReadInsightRows(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return new List<MetaInsightRowDto>();

        return data.EnumerateArray().Select(row => new MetaInsightRowDto
        {
            DateStart = MetaGraphResponseHelper.ReadString(row, "date_start"),
            DateStop = MetaGraphResponseHelper.ReadString(row, "date_stop"),
            Impressions = MetaGraphResponseHelper.ReadString(row, "impressions"),
            Reach = MetaGraphResponseHelper.ReadString(row, "reach"),
            Clicks = MetaGraphResponseHelper.ReadString(row, "clicks"),
            Spend = MetaGraphResponseHelper.ReadString(row, "spend"),
            Ctr = MetaGraphResponseHelper.ReadString(row, "ctr"),
            Cpc = MetaGraphResponseHelper.ReadString(row, "cpc"),
            Cpm = MetaGraphResponseHelper.ReadString(row, "cpm"),
            Actions = row.TryGetProperty("actions", out var actions) ? actions.GetRawText() : null
        }).ToList();
    }

    private static MetaInsightsSummaryDto BuildSummary(
        string objectId,
        string datePreset,
        IReadOnlyList<MetaInsightRowDto> rows)
    {
        decimal Sum(Func<MetaInsightRowDto, string?> selector)
        {
            decimal total = 0;
            foreach (var row in rows)
            {
                var raw = selector(row);
                if (decimal.TryParse(raw, out var value))
                    total += value;
            }

            return total;
        }

        var spend = Sum(r => r.Spend);
        var impressions = Sum(r => r.Impressions);
        var reach = Sum(r => r.Reach);
        var clicks = Sum(r => r.Clicks);
        var ctr = impressions > 0 ? clicks / impressions * 100m : 0m;
        var cpc = clicks > 0 ? spend / clicks : 0m;
        var cpm = impressions > 0 ? spend / impressions * 1000m : 0m;

        return new MetaInsightsSummaryDto
        {
            ObjectId = objectId,
            DatePreset = datePreset,
            Spend = spend.ToString("0.##"),
            Impressions = impressions.ToString("0"),
            Reach = reach.ToString("0"),
            Clicks = clicks.ToString("0"),
            Ctr = ctr.ToString("0.##"),
            Cpc = cpc.ToString("0.##"),
            Cpm = cpm.ToString("0.##"),
            Rows = rows
        };
    }
}
