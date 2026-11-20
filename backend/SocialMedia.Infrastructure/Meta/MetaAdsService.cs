using System.Text.Json;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;

namespace SocialMedia.Infrastructure.Meta;

public class MetaAdsService : IMetaAdsService
{
    private const string AdFields = "id,name,adset_id,campaign_id,status,effective_status,creative{id,object_story_spec}";

    private readonly IMetaGraphApiClient _graph;

    public MetaAdsService(IMetaGraphApiClient graph)
    {
        _graph = graph;
    }

    public Task<Application.DTOs.Common.ApiResponse<IReadOnlyList<MetaAdAccountDto>>> GetAdAccountsAsync(
        Guid userId,
        string menuType,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            using var doc = await _graph.GetAsync(
                userId,
                menuType,
                "me/adaccounts",
                cancellationToken,
                ("fields", "id,name,currency,account_status,business_name,amount_spent,balance"),
                ("limit", "100"));

            return (IReadOnlyList<MetaAdAccountDto>)ReadArray(doc.RootElement, MapAdAccount);
        }, "Ad accounts loaded.");

    public Task<Application.DTOs.Common.ApiResponse<MetaPagedResultDto<MetaCampaignDto>>> GetCampaignsAsync(
        Guid userId,
        string menuType,
        MetaListQuery query,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            var accountId = MetaGraphResponseHelper.NormalizeAdAccountId(query.AdAccountId!);
            var limit = Math.Clamp(query.Limit, 1, 100).ToString();
            var fields = "id,name,objective,status,effective_status,daily_budget,created_time,updated_time,promoted_object";

            using var doc = await _graph.GetAsync(
                userId,
                menuType,
                $"{accountId}/campaigns",
                cancellationToken,
                BuildQuery(
                    ("fields", fields),
                    ("limit", limit),
                    ("after", query.After),
                    ("effective_status", string.IsNullOrWhiteSpace(query.Status) ? null : $"['{query.Status}']")));

            var items = SortCampaigns(ReadArray(doc.RootElement, MapCampaign));

            if (!string.IsNullOrWhiteSpace(query.IncludeCampaignId) &&
                items.All(c => c.Id != query.IncludeCampaignId.Trim()))
            {
                var included = await TryLoadCampaignAsync(userId, menuType, query.IncludeCampaignId.Trim(), cancellationToken);
                if (included is not null)
                    items.Insert(0, included);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim();
                items = items.Where(c =>
                        c.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        c.Id.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return new MetaPagedResultDto<MetaCampaignDto>
            {
                Items = items,
                NextCursor = MetaGraphResponseHelper.ReadNextCursor(doc.RootElement)
            };
        }, "Campaigns loaded.");

    public Task<Application.DTOs.Common.ApiResponse<MetaCampaignDto>> CreateCampaignAsync(
        Guid userId,
        string menuType,
        CreateMetaCampaignRequest request,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            var accountId = MetaGraphResponseHelper.NormalizeAdAccountId(request.AdAccountId);
            var categories = request.SpecialAdCategories?
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToUpperInvariant())
                .ToArray() ?? Array.Empty<string>();

            var payload = new Dictionary<string, string>
            {
                ["name"] = request.Name.Trim(),
                ["objective"] = request.Objective.Trim(),
                ["status"] = string.IsNullOrWhiteSpace(request.Status) ? "PAUSED" : request.Status.Trim().ToUpperInvariant(),
                ["special_ad_categories"] = JsonSerializer.Serialize(categories),
                // Required from Graph API v24+ when campaign budget is set at the ad set level.
                ["is_adset_budget_sharing_enabled"] = "false"
            };

            if (!string.IsNullOrWhiteSpace(request.PageId)
                && request.Objective.Contains("LEAD", StringComparison.OrdinalIgnoreCase))
            {
                payload["promoted_object"] = JsonSerializer.Serialize(new { page_id = request.PageId.Trim() });
            }

            if (categories.Length > 0)
            {
                var countries = request.SpecialAdCategoryCountries?
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c.Trim().ToUpperInvariant())
                    .ToArray() ?? new[] { "US" };
                payload["special_ad_category_country"] = JsonSerializer.Serialize(countries);
            }

            using var doc = await _graph.PostFormAsync(userId, menuType, $"{accountId}/campaigns", payload, cancellationToken);
            var id = MetaGraphResponseHelper.ReadString(doc.RootElement, "id")
                ?? throw new MetaGraphApiException("Meta did not return a campaign id.");

            return await LoadCampaignAsync(userId, menuType, id, cancellationToken);
        }, "Campaign created.");

    public Task<Application.DTOs.Common.ApiResponse<MetaCampaignDto>> UpdateCampaignAsync(
        Guid userId,
        string menuType,
        string campaignId,
        UpdateMetaCampaignRequest request,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            var payload = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(request.Name))
                payload["name"] = request.Name.Trim();
            if (!string.IsNullOrWhiteSpace(request.Status))
                payload["status"] = request.Status.Trim().ToUpperInvariant();

            if (payload.Count == 0)
                throw new MetaGraphApiException("Provide a name or status to update.");

            using var doc = await _graph.PostFormAsync(userId, menuType, campaignId, payload, cancellationToken);
            var id = MetaGraphResponseHelper.ReadString(doc.RootElement, "id") ?? campaignId;

            return await LoadCampaignAsync(userId, menuType, id, cancellationToken);
        }, "Campaign updated.");

    public Task<Application.DTOs.Common.ApiResponse<MetaPagedResultDto<MetaAdSetDto>>> GetAdSetsAsync(
        Guid userId,
        string menuType,
        MetaListQuery query,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            var accountId = MetaGraphResponseHelper.NormalizeAdAccountId(query.AdAccountId!);
            var limit = Math.Clamp(query.Limit, 1, 100).ToString();
            var fields = "id,name,campaign_id,status,effective_status,daily_budget,lifetime_budget,start_time,end_time,targeting";

            var extra = new List<(string, string)>
            {
                ("fields", fields),
                ("limit", limit)
            };
            if (!string.IsNullOrWhiteSpace(query.After))
                extra.Add(("after", query.After));
            if (!string.IsNullOrWhiteSpace(query.CampaignId))
                extra.Add(("campaign_id", query.CampaignId));
            if (!string.IsNullOrWhiteSpace(query.Status))
                extra.Add(("effective_status", $"['{query.Status}']"));

            using var doc = await _graph.GetAsync(
                userId,
                menuType,
                $"{accountId}/adsets",
                cancellationToken,
                extra.ToArray());

            var items = ReadArray(doc.RootElement, MapAdSet);
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim();
                items = items.Where(x =>
                        x.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        x.Id.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return new MetaPagedResultDto<MetaAdSetDto>
            {
                Items = items,
                NextCursor = MetaGraphResponseHelper.ReadNextCursor(doc.RootElement)
            };
        }, "Ad sets loaded.");

    public Task<Application.DTOs.Common.ApiResponse<MetaAdSetDto>> CreateAdSetAsync(
        Guid userId,
        string menuType,
        CreateMetaAdSetRequest request,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            var accountId = MetaGraphResponseHelper.NormalizeAdAccountId(request.AdAccountId);
            var targeting = JsonSerializer.Serialize(new
            {
                geo_locations = new { countries = new[] { "US" } }
            });

            var payload = new Dictionary<string, string>
            {
                ["name"] = request.Name.Trim(),
                ["campaign_id"] = request.CampaignId.Trim(),
                ["daily_budget"] = Math.Max(request.DailyBudget, 100).ToString(),
                ["billing_event"] = request.BillingEvent.Trim().ToUpperInvariant(),
                ["optimization_goal"] = request.OptimizationGoal.Trim().ToUpperInvariant(),
                ["bid_amount"] = "100",
                ["targeting"] = targeting,
                ["status"] = string.IsNullOrWhiteSpace(request.Status) ? "PAUSED" : request.Status.Trim().ToUpperInvariant()
            };

            if (!string.IsNullOrWhiteSpace(request.StartTime))
                payload["start_time"] = request.StartTime;
            if (!string.IsNullOrWhiteSpace(request.EndTime))
                payload["end_time"] = request.EndTime;

            using var doc = await _graph.PostFormAsync(userId, menuType, $"{accountId}/adsets", payload, cancellationToken);
            var id = MetaGraphResponseHelper.ReadString(doc.RootElement, "id")
                ?? throw new MetaGraphApiException("Meta did not return an ad set id.");

            using var loaded = await _graph.GetAsync(
                userId,
                menuType,
                id,
                cancellationToken,
                ("fields", "id,name,campaign_id,status,effective_status,daily_budget,lifetime_budget,start_time,end_time,targeting"));

            return MapAdSet(loaded.RootElement);
        }, "Ad set created.");

    public Task<Application.DTOs.Common.ApiResponse<MetaAdSetDto>> UpdateAdSetAsync(
        Guid userId,
        string menuType,
        string adSetId,
        UpdateMetaAdSetRequest request,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            var payload = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(request.Name))
                payload["name"] = request.Name.Trim();
            if (!string.IsNullOrWhiteSpace(request.Status))
                payload["status"] = request.Status.Trim().ToUpperInvariant();
            if (request.DailyBudget.HasValue)
                payload["daily_budget"] = Math.Max(request.DailyBudget.Value, 100).ToString();

            if (payload.Count == 0)
                throw new MetaGraphApiException("Provide at least one field to update.");

            using var doc = await _graph.PostFormAsync(userId, menuType, adSetId, payload, cancellationToken);
            var id = MetaGraphResponseHelper.ReadString(doc.RootElement, "id") ?? adSetId;

            using var loaded = await _graph.GetAsync(
                userId,
                menuType,
                id,
                cancellationToken,
                ("fields", "id,name,campaign_id,status,effective_status,daily_budget,lifetime_budget,start_time,end_time,targeting"));

            return MapAdSet(loaded.RootElement);
        }, "Ad set updated.");

    public Task<Application.DTOs.Common.ApiResponse<MetaPagedResultDto<MetaAdDto>>> GetAdsAsync(
        Guid userId,
        string menuType,
        MetaListQuery query,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            var accountId = MetaGraphResponseHelper.NormalizeAdAccountId(query.AdAccountId!);
            var limit = Math.Clamp(query.Limit, 1, 100).ToString();
            var fields = AdFields;

            var extra = new List<(string, string)>
            {
                ("fields", fields),
                ("limit", limit)
            };
            if (!string.IsNullOrWhiteSpace(query.After))
                extra.Add(("after", query.After));
            if (!string.IsNullOrWhiteSpace(query.AdSetId))
                extra.Add(("adset_id", query.AdSetId));
            if (!string.IsNullOrWhiteSpace(query.CampaignId))
                extra.Add(("campaign_id", query.CampaignId));
            if (!string.IsNullOrWhiteSpace(query.Status))
                extra.Add(("effective_status", $"['{query.Status}']"));

            using var doc = await _graph.GetAsync(
                userId,
                menuType,
                $"{accountId}/ads",
                cancellationToken,
                extra.ToArray());

            var items = ReadArray(doc.RootElement, MapAd);
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim();
                items = items.Where(x =>
                        x.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        x.Id.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return new MetaPagedResultDto<MetaAdDto>
            {
                Items = items,
                NextCursor = MetaGraphResponseHelper.ReadNextCursor(doc.RootElement)
            };
        }, "Ads loaded.");

    public Task<Application.DTOs.Common.ApiResponse<MetaAdDto>> UpdateAdAsync(
        Guid userId,
        string menuType,
        string adId,
        UpdateMetaAdRequest request,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            var payload = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(request.Name))
                payload["name"] = request.Name.Trim();
            if (!string.IsNullOrWhiteSpace(request.Status))
                payload["status"] = request.Status.Trim().ToUpperInvariant();

            if (payload.Count == 0)
                throw new MetaGraphApiException("Provide a name or status to update.");

            using var doc = await _graph.PostFormAsync(userId, menuType, adId, payload, cancellationToken);
            var id = MetaGraphResponseHelper.ReadString(doc.RootElement, "id") ?? adId;

            using var loaded = await _graph.GetAsync(
                userId,
                menuType,
                id,
                cancellationToken,
                ("fields", AdFields));

            return MapAd(loaded.RootElement);
        }, "Ad updated.");

    public Task<Application.DTOs.Common.ApiResponse<MetaAdDto>> CreateAdAsync(
        Guid userId,
        string menuType,
        CreateMetaAdRequest request,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(request.PageId))
                throw new MetaGraphApiException("Select an authorized Facebook Page to use as the ad identity.");
            if (string.IsNullOrWhiteSpace(request.Link))
                throw new MetaGraphApiException("A destination link is required.");

            var accountId = MetaGraphResponseHelper.NormalizeAdAccountId(request.AdAccountId);
            var message = string.IsNullOrWhiteSpace(request.Message) ? request.Name.Trim() : request.Message.Trim();
            var story = JsonSerializer.Serialize(new
            {
                page_id = request.PageId.Trim(),
                link_data = new
                {
                    message,
                    link = request.Link.Trim(),
                    picture = "https://upload.wikimedia.org/wikipedia/commons/thumb/4/47/PNG_transparency_demonstration_1.png/640px-PNG_transparency_demonstration_1.png",
                    call_to_action = new
                    {
                        type = "LEARN_MORE",
                        value = new { link = request.Link.Trim() }
                    }
                }
            });

            using var creativeDoc = await _graph.PostFormAsync(
                userId,
                menuType,
                $"{accountId}/adcreatives",
                new Dictionary<string, string>
                {
                    ["name"] = $"{request.Name.Trim()} creative",
                    ["object_story_spec"] = story
                },
                cancellationToken);
            var creativeId = RequireCreatedId(creativeDoc.RootElement, "ad creative");

            using var doc = await _graph.PostFormAsync(
                userId,
                menuType,
                $"{accountId}/ads",
                new Dictionary<string, string>
                {
                    ["name"] = request.Name.Trim(),
                    ["adset_id"] = request.AdSetId.Trim(),
                    ["status"] = "PAUSED",
                    ["creative"] = JsonSerializer.Serialize(new { creative_id = creativeId })
                },
                cancellationToken);
            var id = RequireCreatedId(doc.RootElement, "ad");

            using var loaded = await _graph.GetAsync(
                userId,
                menuType,
                id,
                cancellationToken,
                ("fields", AdFields));

            var ad = MapAd(loaded.RootElement);
            if (!string.IsNullOrWhiteSpace(ad.PageId)
                && !string.Equals(ad.PageId, request.PageId.Trim(), StringComparison.Ordinal))
                throw new MetaGraphApiException("The ad was created, but the Facebook Page on the creative does not match the page you selected.");

            return ad;
        }, "Paused ad created with the selected Facebook Page.");

    public Task<Application.DTOs.Common.ApiResponse<MetaAdPreviewDto>> GetAdPreviewAsync(
        Guid userId,
        string menuType,
        string adId,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            using var doc = await _graph.GetAsync(
                userId,
                menuType,
                $"{adId}/previews",
                cancellationToken,
                ("ad_format", "DESKTOP_FEED_STANDARD"));

            string? html = null;
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                var first = data.EnumerateArray().FirstOrDefault();
                html = MetaGraphResponseHelper.ReadString(first, "body");
            }

            return new MetaAdPreviewDto { Html = html };
        }, "Ad preview loaded.");

    private static (string Key, string Value)[] BuildQuery(params (string Key, string? Value)[] items) =>
        items
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => (item.Key, item.Value!))
            .ToArray();

    private static List<T> ReadArray<T>(JsonElement root, Func<JsonElement, T> map)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return new List<T>();

        return data.EnumerateArray().Select(map).ToList();
    }

    private async Task<MetaCampaignDto> LoadCampaignAsync(
        Guid userId,
        string menuType,
        string campaignId,
        CancellationToken cancellationToken)
    {
        using var loaded = await _graph.GetAsync(
            userId,
            menuType,
            campaignId,
            cancellationToken,
            ("fields", "id,name,objective,status,effective_status,daily_budget,created_time,updated_time"));

        return MapCampaign(loaded.RootElement);
    }

    private async Task<MetaCampaignDto?> TryLoadCampaignAsync(
        Guid userId,
        string menuType,
        string campaignId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await LoadCampaignAsync(userId, menuType, campaignId, cancellationToken);
        }
        catch (MetaGraphApiException)
        {
            return null;
        }
    }

    private static List<MetaCampaignDto> SortCampaigns(IReadOnlyList<MetaCampaignDto> items) =>
        items
            .OrderByDescending(c => ParseMetaDate(c.UpdatedTime))
            .ThenByDescending(c => ParseMetaDate(c.CreatedTime))
            .ThenByDescending(c => c.Id, StringComparer.Ordinal)
            .ToList();

    private static DateTimeOffset ParseMetaDate(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;

    private static MetaAdAccountDto MapAdAccount(JsonElement row) => new()
    {
        Id = MetaGraphResponseHelper.ReadString(row, "id") ?? string.Empty,
        Name = MetaGraphResponseHelper.ReadString(row, "name") ?? "Ad Account",
        Currency = MetaGraphResponseHelper.ReadString(row, "currency"),
        AccountStatus = FormatAccountStatus(MetaGraphResponseHelper.ReadString(row, "account_status")),
        BusinessName = MetaGraphResponseHelper.ReadString(row, "business_name"),
        AmountSpent = FormatMinorCurrency(MetaGraphResponseHelper.ReadString(row, "amount_spent")),
        Balance = FormatMinorCurrency(MetaGraphResponseHelper.ReadString(row, "balance"))
    };

    private static string? FormatAccountStatus(string? raw) =>
        raw switch
        {
            "1" => "ACTIVE",
            "2" => "DISABLED",
            "3" => "UNSETTLED",
            "7" => "PENDING_RISK_REVIEW",
            "8" => "PENDING_SETTLEMENT",
            "9" => "IN_GRACE_PERIOD",
            "100" => "PENDING_CLOSURE",
            "101" => "CLOSED",
            _ => raw
        };

    private static string? FormatMinorCurrency(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        if (!decimal.TryParse(raw, out var value))
            return raw;

        return (value / 100m).ToString("0.##");
    }

    private static MetaCampaignDto MapCampaign(JsonElement row) => new()
    {
        Id = MetaGraphResponseHelper.ReadString(row, "id") ?? string.Empty,
        Name = MetaGraphResponseHelper.ReadString(row, "name") ?? string.Empty,
        Objective = MetaGraphResponseHelper.ReadString(row, "objective"),
        Status = MetaGraphResponseHelper.ReadString(row, "status"),
        EffectiveStatus = MetaGraphResponseHelper.ReadString(row, "effective_status"),
        DailyBudget = FormatMinorCurrency(MetaGraphResponseHelper.ReadString(row, "daily_budget")),
        CreatedTime = MetaGraphResponseHelper.ReadString(row, "created_time"),
        UpdatedTime = MetaGraphResponseHelper.ReadString(row, "updated_time"),
        PageId = row.TryGetProperty("promoted_object", out var promoted)
            ? MetaGraphResponseHelper.ReadString(promoted, "page_id")
            : null
    };

    private static MetaAdSetDto MapAdSet(JsonElement row)
    {
        string? targetingSummary = null;
        string? placements = null;
        if (row.TryGetProperty("targeting", out var targeting) && targeting.ValueKind == JsonValueKind.Object)
        {
            var ageMin = MetaGraphResponseHelper.ReadString(targeting, "age_min");
            var ageMax = MetaGraphResponseHelper.ReadString(targeting, "age_max");
            var countries = string.Empty;
            if (targeting.TryGetProperty("geo_locations", out var geo)
                && geo.TryGetProperty("countries", out var countryList)
                && countryList.ValueKind == JsonValueKind.Array)
            {
                countries = string.Join(", ", countryList.EnumerateArray().Select(item => item.ToString()));
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(ageMin) || !string.IsNullOrWhiteSpace(ageMax))
                parts.Add($"Ages {ageMin ?? "—"}-{ageMax ?? "—"}");
            if (!string.IsNullOrWhiteSpace(countries))
                parts.Add(countries);
            targetingSummary = parts.Count == 0 ? null : string.Join("; ", parts);

            if (targeting.TryGetProperty("publisher_platforms", out var platforms)
                && platforms.ValueKind == JsonValueKind.Array)
            {
                placements = string.Join(", ", platforms.EnumerateArray().Select(item => item.ToString()));
            }
        }

        return new MetaAdSetDto
        {
            Id = MetaGraphResponseHelper.ReadString(row, "id") ?? string.Empty,
            Name = MetaGraphResponseHelper.ReadString(row, "name") ?? string.Empty,
            CampaignId = MetaGraphResponseHelper.ReadString(row, "campaign_id"),
            Status = MetaGraphResponseHelper.ReadString(row, "status"),
            EffectiveStatus = MetaGraphResponseHelper.ReadString(row, "effective_status"),
            DailyBudget = MetaGraphResponseHelper.ReadString(row, "daily_budget"),
            LifetimeBudget = MetaGraphResponseHelper.ReadString(row, "lifetime_budget"),
            StartTime = MetaGraphResponseHelper.ReadString(row, "start_time"),
            EndTime = MetaGraphResponseHelper.ReadString(row, "end_time"),
            TargetingSummary = targetingSummary,
            Placements = placements
        };
    }

    private static string RequireCreatedId(JsonElement root, string label)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var error))
        {
            var message = MetaGraphResponseHelper.ReadString(error, "message");
            throw new MetaGraphApiException(string.IsNullOrWhiteSpace(message)
                ? $"Meta rejected the {label}."
                : message);
        }

        var id = FirstId(root);
        if (!string.IsNullOrWhiteSpace(id))
            return id;

        var body = root.GetRawText();
        if (body.Length > 500)
            body = body[..500];
        throw new MetaGraphApiException($"Meta did not return a {label} id. Response: {body}");
    }

    private static string? FirstId(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var name in new[] { "id", "creative_id", "ad_id" })
        {
            var value = MetaGraphResponseHelper.ReadString(root, name);
            if (!string.IsNullOrWhiteSpace(value) && !string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
                return value;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
                continue;
            var nested = FirstId(property.Value);
            if (!string.IsNullOrWhiteSpace(nested))
                return nested;
        }

        return null;
    }

    private static MetaAdDto MapAd(JsonElement row)
    {
        string? creativeId = null;
        string? pageId = null;
        if (row.TryGetProperty("creative", out var creative) && creative.ValueKind == JsonValueKind.Object)
        {
            if (creative.TryGetProperty("id", out var creativeIdEl))
                creativeId = creativeIdEl.ToString();
            if (creative.TryGetProperty("object_story_spec", out var story)
                && story.ValueKind == JsonValueKind.Object)
                pageId = MetaGraphResponseHelper.ReadString(story, "page_id");
        }

        return new MetaAdDto
        {
            Id = MetaGraphResponseHelper.ReadString(row, "id") ?? string.Empty,
            Name = MetaGraphResponseHelper.ReadString(row, "name") ?? string.Empty,
            AdSetId = MetaGraphResponseHelper.ReadString(row, "adset_id"),
            CampaignId = MetaGraphResponseHelper.ReadString(row, "campaign_id"),
            Status = MetaGraphResponseHelper.ReadString(row, "status"),
            EffectiveStatus = MetaGraphResponseHelper.ReadString(row, "effective_status"),
            CreativeId = creativeId,
            PageId = pageId
        };
    }
}
