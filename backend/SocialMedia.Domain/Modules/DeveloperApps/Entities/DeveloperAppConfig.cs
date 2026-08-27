using SocialMedia.Domain.Common;
using SocialMedia.Domain.Entities;

namespace SocialMedia.Domain.Modules.DeveloperApps.Entities;

/// <summary>
/// Developer Apps process app credentials — table: DeveloperAppConfigs.
/// </summary>
public class DeveloperAppConfig : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid PlatformId { get; set; }
    public string PlatformCode { get; set; } = string.Empty;
    public string MenuType { get; set; } = "developer_app";

    public string? Label { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string? RedirectUri { get; set; }
    public string? AuthUrl { get; set; }
    public string? BaseUrl { get; set; }
    public string? Scopes { get; set; }
    public string GraphApiVersion { get; set; } = "v21.0";
    public string? WebhookVerifyToken { get; set; }
    public string? PhoneNumberId { get; set; }
    public string? WabaId { get; set; }

    public User? User { get; set; }
    public Platform? Platform { get; set; }
}
