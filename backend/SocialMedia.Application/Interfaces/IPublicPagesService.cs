using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.PublicPages;

namespace SocialMedia.Application.Interfaces;

public interface IPublicPagesService
{
    /// <summary>
    /// Lists Facebook Pages from Graph <c>me/accounts</c> for this module's connected Facebook user,
    /// then filters by the search keyword. Results are not stored.
    /// </summary>
    Task<ApiResponse<IReadOnlyList<PublicPageDto>>> SearchAsync(
        Guid userId,
        string? menuType,
        string? query,
        CancellationToken cancellationToken = default);
}
