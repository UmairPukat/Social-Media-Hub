namespace SocialMedia.Application.DTOs.Process;

public class ProcessAppConfigDto
{
    public Guid Id { get; set; }
    public Guid PlatformId { get; set; }
    public string PlatformCode { get; set; } = string.Empty;
    public string MenuType { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string ClientId { get; set; } = string.Empty;
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

public class SaveProcessAppConfigRequest
{
    public string PlatformCode { get; set; } = string.Empty;
    public string MenuType { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string ClientId { get; set; } = string.Empty;
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
