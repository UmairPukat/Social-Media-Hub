using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.Meta;

namespace SocialMedia.Application.Interfaces;

public interface IMetaCatalogService
{
    Task<ApiResponse<MetaPagedResultDto<MetaCatalogDto>>> GetCatalogsAsync(
        Guid userId,
        string menuType,
        MetaCatalogListQuery query,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MetaCatalogDto>> CreateCatalogAsync(
        Guid userId,
        string menuType,
        CreateMetaCatalogRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MetaPagedResultDto<MetaProductDto>>> GetProductsAsync(
        Guid userId,
        string menuType,
        MetaProductListQuery query,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MetaProductDto>> CreateProductAsync(
        Guid userId,
        string menuType,
        CreateMetaProductRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MetaProductDto>> UpdateProductAsync(
        Guid userId,
        string menuType,
        string catalogId,
        string productId,
        UpdateMetaProductRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<object>> DeleteProductAsync(
        Guid userId,
        string menuType,
        string catalogId,
        string productId,
        CancellationToken cancellationToken = default);
}
