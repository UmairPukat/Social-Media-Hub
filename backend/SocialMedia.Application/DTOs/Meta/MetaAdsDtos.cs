using System.ComponentModel.DataAnnotations;

namespace SocialMedia.Application.DTOs.Meta;

public class MetaPagedResultDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public string? NextCursor { get; set; }
    public int Count => Items.Count;
}

public class MetaAdAccountDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Currency { get; set; }
    public string? AccountStatus { get; set; }
    public string? BusinessName { get; set; }
}

public class MetaCampaignDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Objective { get; set; }
    public string? Status { get; set; }
    public string? EffectiveStatus { get; set; }
    public string? CreatedTime { get; set; }
    public string? UpdatedTime { get; set; }
}

public class MetaAdSetDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CampaignId { get; set; }
    public string? Status { get; set; }
    public string? EffectiveStatus { get; set; }
    public string? DailyBudget { get; set; }
    public string? LifetimeBudget { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
}

public class MetaAdDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AdSetId { get; set; }
    public string? CampaignId { get; set; }
    public string? Status { get; set; }
    public string? EffectiveStatus { get; set; }
    public string? CreativeId { get; set; }
}

public class CreateMetaCampaignRequest
{
    [Required]
    public string AdAccountId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Objective { get; set; } = "OUTCOME_AWARENESS";

    public string Status { get; set; } = "PAUSED";

    public IReadOnlyList<string>? SpecialAdCategories { get; set; }

    public IReadOnlyList<string>? SpecialAdCategoryCountries { get; set; }
}

public class UpdateMetaCampaignRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

    public string? Status { get; set; }
}

public class CreateMetaAdSetRequest
{
    [Required]
    public string AdAccountId { get; set; } = string.Empty;

    [Required]
    public string CampaignId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = "PAUSED";

    public long DailyBudget { get; set; } = 1000;

    public string BillingEvent { get; set; } = "IMPRESSIONS";

    public string OptimizationGoal { get; set; } = "REACH";

    public string? StartTime { get; set; }

    public string? EndTime { get; set; }
}

public class UpdateMetaAdSetRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

    public string? Status { get; set; }

    public long? DailyBudget { get; set; }
}

public class UpdateMetaAdRequest
{
    public string? Name { get; set; }
    public string? Status { get; set; }
}

public class MetaListQuery
{
    public string? AdAccountId { get; set; }
    public string? CampaignId { get; set; }
    public string? AdSetId { get; set; }
    public string? Status { get; set; }
    public string? Search { get; set; }
    public string? After { get; set; }
    public int Limit { get; set; } = 25;
}
