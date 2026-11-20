using System.ComponentModel.DataAnnotations;

namespace SocialMedia.Application.DTOs.Meta;

public static class FacebookLeadStatuses
{
    public static readonly string[] All = ["New", "Contacted", "Qualified", "Converted", "Lost"];

    public static bool IsAllowed(string? status)
        => !string.IsNullOrWhiteSpace(status)
           && All.Any(item => string.Equals(item, status.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string status)
        => All.First(item => string.Equals(item, status.Trim(), StringComparison.OrdinalIgnoreCase));
}

public class MetaPageOptionDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class MetaLeadgenSubscriptionDto
{
    public bool Subscribed { get; set; }
}

public class MetaLeadFormDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? CreatedTime { get; set; }
}

public class MetaLeadFieldDto
{
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
}

public class MetaLeadDto
{
    public string Id { get; set; } = string.Empty;
    public string? CreatedTime { get; set; }
    public string? FormId { get; set; }
    public string? FormName { get; set; }
    public string? PageId { get; set; }
    public string? PageName { get; set; }
    public string? AdId { get; set; }
    public string? AdName { get; set; }
    public string? CampaignId { get; set; }
    public string? CampaignName { get; set; }
    public string? CustomerName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = "New";
    public string? Notes { get; set; }
    public string? Source { get; set; }
    public IReadOnlyList<MetaLeadFieldDto> Fields { get; set; } = Array.Empty<MetaLeadFieldDto>();
}

public class UpdateMetaLeadRequest
{
    public string? Status { get; set; }

    [MaxLength(4000)]
    public string? Notes { get; set; }
}

public class CreateMetaAdRequest
{
    [Required]
    public string AdAccountId { get; set; } = string.Empty;

    [Required]
    public string AdSetId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string PageId { get; set; } = string.Empty;

    [Required]
    public string Link { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Message { get; set; }
}

public class MetaAdPreviewDto
{
    public string? Html { get; set; }
}
