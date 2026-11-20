using SocialMedia.Domain.Common;

namespace SocialMedia.Domain.Entities;

/// <summary>
/// A Facebook Lead Ad stored for one user inside one process module.
/// Graph payloads are refreshed in place; status and notes stay local.
/// </summary>
public class FacebookLead : BaseEntity
{
    public Guid UserId { get; set; }
    public string MenuType { get; set; } = string.Empty;
    public string PageId { get; set; } = string.Empty;
    public string? PageName { get; set; }
    public string? FormId { get; set; }
    public string? FormName { get; set; }
    public string ExternalLeadId { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public string? CustomerName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CampaignId { get; set; }
    public string? CampaignName { get; set; }
    public string? AdId { get; set; }
    public string? AdName { get; set; }
    public string FieldDataJson { get; set; } = "[]";
    public string Status { get; set; } = "New";
    public string? Notes { get; set; }
    public string Source { get; set; } = "graph";
}
