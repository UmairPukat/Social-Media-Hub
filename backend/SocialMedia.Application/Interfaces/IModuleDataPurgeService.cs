using SocialMedia.Application.DTOs.Common;

namespace SocialMedia.Application.Interfaces;

public interface IModuleDataPurgeService
{
    /// <summary>
    /// Permanently deletes inbox, post, and connected-account data for one process menu,
    /// or every menu when <paramref name="menuType"/> is <c>all</c>.
    /// Users, platform catalog rows, and app credentials are kept.
    /// </summary>
    Task<ApiResponse<ModulePurgeResultDto>> PurgeAsync(string menuType, CancellationToken cancellationToken = default);
}

public class ModulePurgeResultDto
{
    public string MenuType { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int DeletedRows { get; set; }
}
