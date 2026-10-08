using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.PublicPages;

namespace SocialMedia.Application.Interfaces;

public interface IPublicPagesService
{
    /// <summary>
    /// Live Facebook Pages Search using this module's Facebook app token.
    /// Results are not stored.
    /// </summary>
    Task<ApiResponse<IReadOnlyList<PublicPageDto>>> SearchAsync(
        Guid userId,
        string? menuType,
        string? query,
        CancellationToken cancellationToken = default);
}
