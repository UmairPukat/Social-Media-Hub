using System.Text.Json;
using SocialMedia.Application.DTOs.Meta;

namespace SocialMedia.Application.Meta;

public static class MetaUsageHeaderParser
{
    public static MetaUsageSnapshotDto Parse(
        string? businessUseCaseUsage,
        string? adAccountUsage,
        string? appUsage)
    {
        var buc = ReadMaxPercent(businessUseCaseUsage);
        var adAccount = ReadMaxPercent(adAccountUsage);
        var app = ReadMaxPercent(appUsage);
        var overall = Math.Max(buc, Math.Max(adAccount, app));

        return new MetaUsageSnapshotDto
        {
            BusinessUseCasePercent = buc,
            AdAccountPercent = adAccount,
            AppPercent = app,
            OverallPercent = overall,
            IsApproachingLimit = overall >= 80,
            RawBusinessUseCaseUsage = businessUseCaseUsage,
            RawAdAccountUsage = adAccountUsage,
            RawAppUsage = appUsage
        };
    }

    private static int ReadMaxPercent(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(headerValue);
            var max = 0;
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var bucket in property.Value.EnumerateArray())
                {
                    if (bucket.TryGetProperty("call_count", out var callCount) &&
                        bucket.TryGetProperty("total_time", out var totalTime) &&
                        totalTime.GetInt32() > 0)
                    {
                        var percent = (int)Math.Round(callCount.GetInt32() * 100.0 / totalTime.GetInt32());
                        max = Math.Max(max, percent);
                    }
                    else if (bucket.TryGetProperty("acc_id_util_pct", out var util))
                    {
                        max = Math.Max(max, (int)Math.Round(util.GetDouble()));
                    }
                }
            }

            return Math.Clamp(max, 0, 100);
        }
        catch (JsonException)
        {
            return 0;
        }
    }
}
