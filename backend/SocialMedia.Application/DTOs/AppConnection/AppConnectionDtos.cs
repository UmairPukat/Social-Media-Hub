using SocialMedia.Application.Catalog;

namespace SocialMedia.Application.DTOs.AppConnection;

public class AppConnectionConfigDto
{
    public Guid Id { get; set; }
    public Guid PlatformId { get; set; }
    public string PlatformCode { get; set; } = string.Empty;
    public string MenuType { get; set; } = MenuTypes.AppConnection;
    public string? Label { get; set; }
    public string ClientId { get; set; } = string.Empty;
    /// <summary>Masked unless the client explicitly requests reveal.</summary>
    public string? ClientSecret { get; set; }
    public bool HasClientSecret { get; set; }
    public string? RedirectUri { get; set; }
    public string? AuthUrl { get; set; }
    public string? BaseUrl { get; set; }
    public string? Scopes { get; set; }
    public string GraphApiVersion { get; set; } = "v21.0";
    public string? WebhookVerifyToken { get; set; }
    public string? PhoneNumberId { get; set; }
    public string? WabaId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class SaveAppConnectionConfigRequest
{
    public string PlatformCode { get; set; } = string.Empty;
    public string MenuType { get; set; } = MenuTypes.AppConnection;
    public string? Label { get; set; }
    public string ClientId { get; set; } = string.Empty;
    /// <summary>Leave empty on update to keep the stored secret.</summary>
    public string? ClientSecret { get; set; }
    public string? RedirectUri { get; set; }
    public string? AuthUrl { get; set; }
    public string? BaseUrl { get; set; }
    public string? Scopes { get; set; }
    public string? GraphApiVersion { get; set; }
    public string? WebhookVerifyToken { get; set; }
    public string? PhoneNumberId { get; set; }
    public string? WabaId { get; set; }
}
