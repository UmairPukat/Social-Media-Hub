using SocialMedia.Application.DTOs.Integration;
using SocialMedia.Domain.Enums;

namespace SocialMedia.Application.DTOs.AppConnections;

public class MetaAppConnectionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PlatformCode { get; set; } = string.Empty;
    public string PlatformName { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string GraphApiVersion { get; set; } = "v21.0";
    public string Scopes { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public string? AccountName { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public bool SupportsComments { get; set; }
    public bool SupportsMessages { get; set; }
    public bool SupportsPosts { get; set; }
    public bool CanConnect { get; set; }
    public bool RequiresPageSelection { get; set; }
}

public class CreateMetaAppConnectionRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PlatformCode { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public string? GraphApiVersion { get; set; }
    public string? Scopes { get; set; }
}

public class UpdateMetaAppConnectionRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public string? GraphApiVersion { get; set; }
    public string? Scopes { get; set; }
}

public class AppConnectionDefaultScopesDto
{
    public string PlatformCode { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
}

public class BeginAppConnectionOAuthRequest
{
    public Guid AppConnectionId { get; set; }
}

public class BeginAppConnectionOAuthResponse
{
    public string AuthUrl { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string PlatformCode { get; set; } = string.Empty;
    public Guid AppConnectionId { get; set; }
}

public class AppConnectionMetaRedirectResult
{
    public bool Ok { get; set; }
    public string PlatformCode { get; set; } = string.Empty;
    public Guid AppConnectionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<string> FrontendOrigins { get; set; } = Array.Empty<string>();
}

public class AppConnectionSelectPageRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("appConnectionId")]
    public Guid AppConnectionId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("pageId")]
    public string PageId { get; set; } = string.Empty;
}

public class AppConnectionConnectionDetailsDto
{
    public Guid AppConnectionId { get; set; }
    public string AppConnectionName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PlatformCode { get; set; } = string.Empty;
    public string PlatformName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public SocialAccountStatus Status { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? PageId { get; set; }
    public string? PageName { get; set; }
    public string? PageImage { get; set; }
    public string? InstagramId { get; set; }
    public string? InstagramUsername { get; set; }
    public string? AccessToken { get; set; }
    public bool WebhookSubscribed { get; set; }
    public IReadOnlyList<string> SubscribedFields { get; set; } = Array.Empty<string>();
    public string? WebhookError { get; set; }
    public string AppId { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public IReadOnlyList<SocialProfileDto> Profiles { get; set; } = Array.Empty<SocialProfileDto>();
}
