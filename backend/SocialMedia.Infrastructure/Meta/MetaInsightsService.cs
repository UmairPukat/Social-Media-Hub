using System.Text.Json;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;

namespace SocialMedia.Infrastructure.Meta;

public class MetaInsightsService : IMetaInsightsService
{
    private const string DefaultInsightFields =
        "campaign_name,adset_name,ad_name,impressions,reach,clicks,spend,ctr,cpc,cpm,cpp,actions,purchase_roas,date_start,date_stop";

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

            var level = string.IsNullOrWhiteSpace(query.Level) ? "campaign" : query.Level.Trim().ToLowerInvariant();
            var fields = string.IsNullOrWhiteSpace(query.Fields) ? DefaultInsightFields : query.Fields.Trim();
            var queryParams = new List<(string, string)>
            {
                ("fields", fields),
                ("level", level)
            };

            if (level is "campaign" or "adset" or "ad")
            {
                queryParams.Add(("time_increment", "all_days"));
            }
            else
            {
                queryParams.Add(("time_increment", "1"));
            }

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
            return BuildSummary(
                query.ObjectId.Trim(),
                string.IsNullOrWhiteSpace(query.DatePreset) ? "last_7d" : query.DatePreset,
                level,
                rows);
        }, "Insights loaded.");

    private static List<MetaInsightRowDto> ReadInsightRows(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return new List<MetaInsightRowDto>();

        return data.EnumerateArray().Select(row => new MetaInsightRowDto
        {
            CampaignName = MetaGraphResponseHelper.ReadString(row, "campaign_name"),
            AdSetName = MetaGraphResponseHelper.ReadString(row, "adset_name"),
            AdName = MetaGraphResponseHelper.ReadString(row, "ad_name"),
            DateStart = MetaGraphResponseHelper.ReadString(row, "date_start"),
            DateStop = MetaGraphResponseHelper.ReadString(row, "date_stop"),
            Impressions = MetaGraphResponseHelper.ReadString(row, "impressions"),
            Reach = MetaGraphResponseHelper.ReadString(row, "reach"),
            Clicks = MetaGraphResponseHelper.ReadString(row, "clicks"),
            Spend = MetaGraphResponseHelper.ReadString(row, "spend"),
            Ctr = MetaGraphResponseHelper.ReadString(row, "ctr"),
            Cpc = MetaGraphResponseHelper.ReadString(row, "cpc"),
            Cpm = MetaGraphResponseHelper.ReadString(row, "cpm"),
            Cpp = MetaGraphResponseHelper.ReadString(row, "cpp"),
            Actions = row.TryGetProperty("actions", out var actions) ? actions.GetRawText() : null,
            PurchaseRoas = ReadPurchaseRoas(row),
            Results = ReadResults(row)
        }).ToList();
    }

    private static string? ReadPurchaseRoas(JsonElement row)
    {
        if (!row.TryGetProperty("purchase_roas", out var roas) || roas.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var item in roas.EnumerateArray())
        {
            if (item.TryGetProperty("value", out var value))
                return value.ToString();
        }

        return null;
    }

    private static string ReadResults(JsonElement row)
    {
        if (!row.TryGetProperty("actions", out var actions) || actions.ValueKind != JsonValueKind.Array)
            return "0";

        decimal total = 0;
        foreach (var action in actions.EnumerateArray())
        {
            if (!action.TryGetProperty("value", out var valueEl))
                continue;

            if (decimal.TryParse(valueEl.ToString(), out var value))
                total += value;
        }

        return total.ToString("0");
    }

    private static MetaInsightsSummaryDto BuildSummary(
        string objectId,
        string datePreset,
        string level,
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
        var cpp = reach > 0 ? spend / reach * 1000m : 0m;
        var results = Sum(r => r.Results);

        return new MetaInsightsSummaryDto
        {
            ObjectId = objectId,
            DatePreset = datePreset,
            Level = level,
            Spend = spend.ToString("0.##"),
            Impressions = impressions.ToString("0"),
            Reach = reach.ToString("0"),
            Clicks = clicks.ToString("0"),
            Ctr = ctr.ToString("0.##"),
            Cpc = cpc.ToString("0.##"),
            Cpm = cpm.ToString("0.##"),
            Cpp = cpp.ToString("0.##"),
            Results = results.ToString("0"),
            Rows = rows
        };
    }
}
