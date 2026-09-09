using System.ComponentModel.DataAnnotations;

namespace SocialMedia.Application.DTOs.Meta;

public class MetaInsightsQuery
{
    [Required]
    public string ObjectId { get; set; } = string.Empty;

    public string DatePreset { get; set; } = "last_7d";

    public string? Since { get; set; }

    public string? Until { get; set; }

    public string Level { get; set; } = "campaign";
}

public class MetaInsightRowDto
{
    public string? DateStart { get; set; }
    public string? DateStop { get; set; }
    public string? Impressions { get; set; }
    public string? Reach { get; set; }
    public string? Clicks { get; set; }
    public string? Spend { get; set; }
    public string? Ctr { get; set; }
    public string? Cpc { get; set; }
    public string? Cpm { get; set; }
    public string? Actions { get; set; }
}

public class MetaInsightsSummaryDto
{
    public string ObjectId { get; set; } = string.Empty;
    public string DatePreset { get; set; } = string.Empty;
    public string Spend { get; set; } = "0";
    public string Impressions { get; set; } = "0";
    public string Reach { get; set; } = "0";
    public string Clicks { get; set; } = "0";
    public string Ctr { get; set; } = "0";
    public string Cpc { get; set; } = "0";
    public string Cpm { get; set; } = "0";
    public IReadOnlyList<MetaInsightRowDto> Rows { get; set; } = Array.Empty<MetaInsightRowDto>();
}
