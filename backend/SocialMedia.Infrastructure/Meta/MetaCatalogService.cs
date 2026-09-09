using System.Text.Json;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;

namespace SocialMedia.Infrastructure.Meta;

public class MetaCatalogService : IMetaCatalogService
{
    private readonly IMetaGraphApiClient _graph;

    public MetaCatalogService(IMetaGraphApiClient graph)
    {
        _graph = graph;
    }

    public Task<Application.DTOs.Common.ApiResponse<MetaPagedResultDto<MetaCatalogDto>>> GetCatalogsAsync(
        Guid userId,
        string menuType,
        MetaCatalogListQuery query,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            using var businessesDoc = await _graph.GetAsync(
                userId,
                menuType,
                "me/businesses",
                cancellationToken,
                ("fields", "id,name"),
                ("limit", "25"));

            var catalogs = new List<MetaCatalogDto>();
            if (businessesDoc.RootElement.TryGetProperty("data", out var businesses))
            {
                foreach (var business in businesses.EnumerateArray())
                {
                    var businessId = MetaGraphResponseHelper.ReadString(business, "id");
                    if (string.IsNullOrWhiteSpace(businessId))
                        continue;

                    using var catalogDoc = await _graph.GetAsync(
                        userId,
                        menuType,
                        $"{businessId}/owned_product_catalogs",
                        cancellationToken,
                        ("fields", "id,name,vertical,product_count"),
                        ("limit", Math.Clamp(query.Limit, 1, 100).ToString()),
                        ("after", query.After ?? string.Empty));

                    if (!catalogDoc.RootElement.TryGetProperty("data", out var data))
                        continue;

                    foreach (var row in data.EnumerateArray())
                    {
                        catalogs.Add(new MetaCatalogDto
                        {
                            Id = MetaGraphResponseHelper.ReadString(row, "id") ?? string.Empty,
                            Name = MetaGraphResponseHelper.ReadString(row, "name") ?? "Catalog",
                            Vertical = MetaGraphResponseHelper.ReadString(row, "vertical"),
                            ProductCount = MetaGraphResponseHelper.ReadString(row, "product_count"),
                            BusinessId = businessId
                        });
                    }
                }
            }

            return new MetaPagedResultDto<MetaCatalogDto>
            {
                Items = catalogs,
                NextCursor = null
            };
        }, "Catalogs loaded.");

    public Task<Application.DTOs.Common.ApiResponse<MetaPagedResultDto<MetaProductDto>>> GetProductsAsync(
        Guid userId,
        string menuType,
        MetaProductListQuery query,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            var limit = Math.Clamp(query.Limit, 1, 100).ToString();
            var extra = new List<(string, string)>
            {
                ("fields", "id,name,retailer_id,price,currency,availability,image_url,url,review_status"),
                ("limit", limit)
            };
            if (!string.IsNullOrWhiteSpace(query.After))
                extra.Add(("after", query.After));

            using var doc = await _graph.GetAsync(
                userId,
                menuType,
                $"{query.CatalogId.Trim()}/products",
                cancellationToken,
                extra.ToArray());

            var items = ReadProducts(doc.RootElement);
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim();
                items = items.Where(p =>
                        p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        (p.RetailerId?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();
            }

            return new MetaPagedResultDto<MetaProductDto>
            {
                Items = items,
                NextCursor = MetaGraphResponseHelper.ReadNextCursor(doc.RootElement)
            };
        }, "Products loaded.");

    public Task<Application.DTOs.Common.ApiResponse<MetaProductDto>> CreateProductAsync(
        Guid userId,
        string menuType,
        CreateMetaProductRequest request,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            var payload = new Dictionary<string, string>
            {
                ["name"] = request.Name.Trim(),
                ["retailer_id"] = request.RetailerId.Trim(),
                ["price"] = request.Price.Trim(),
                ["currency"] = request.Currency.Trim().ToUpperInvariant(),
                ["availability"] = request.Availability.Trim(),
                ["condition"] = "new"
            };

            if (!string.IsNullOrWhiteSpace(request.Url))
                payload["url"] = request.Url.Trim();
            if (!string.IsNullOrWhiteSpace(request.ImageUrl))
                payload["image_url"] = request.ImageUrl.Trim();
            if (!string.IsNullOrWhiteSpace(request.Description))
                payload["description"] = request.Description.Trim();

            using var doc = await _graph.PostFormAsync(
                userId,
                menuType,
                $"{request.CatalogId.Trim()}/products",
                payload,
                cancellationToken);

            var id = MetaGraphResponseHelper.ReadString(doc.RootElement, "id")
                ?? throw new MetaGraphApiException("Meta did not return a product id.");

            using var loaded = await _graph.GetAsync(
                userId,
                menuType,
                id,
                cancellationToken,
                ("fields", "id,name,retailer_id,price,currency,availability,image_url,url,review_status"));

            return MapProduct(loaded.RootElement);
        }, "Product created.");

    public Task<Application.DTOs.Common.ApiResponse<MetaProductDto>> UpdateProductAsync(
        Guid userId,
        string menuType,
        string catalogId,
        string productId,
        UpdateMetaProductRequest request,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            var payload = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(request.Name))
                payload["name"] = request.Name.Trim();
            if (!string.IsNullOrWhiteSpace(request.Price))
                payload["price"] = request.Price.Trim();
            if (!string.IsNullOrWhiteSpace(request.Currency))
                payload["currency"] = request.Currency.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(request.Availability))
                payload["availability"] = request.Availability.Trim();
            if (!string.IsNullOrWhiteSpace(request.Url))
                payload["url"] = request.Url.Trim();
            if (!string.IsNullOrWhiteSpace(request.ImageUrl))
                payload["image_url"] = request.ImageUrl.Trim();
            if (!string.IsNullOrWhiteSpace(request.Description))
                payload["description"] = request.Description.Trim();

            if (payload.Count == 0)
                throw new MetaGraphApiException("Provide at least one field to update.");

            using var doc = await _graph.PostFormAsync(userId, menuType, productId, payload, cancellationToken);
            var id = MetaGraphResponseHelper.ReadString(doc.RootElement, "id") ?? productId;

            using var loaded = await _graph.GetAsync(
                userId,
                menuType,
                id,
                cancellationToken,
                ("fields", "id,name,retailer_id,price,currency,availability,image_url,url,review_status"));

            return MapProduct(loaded.RootElement);
        }, "Product updated.");

    public Task<Application.DTOs.Common.ApiResponse<object>> DeleteProductAsync(
        Guid userId,
        string menuType,
        string catalogId,
        string productId,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync<object>(async () =>
        {
            await _graph.DeleteAsync(userId, menuType, productId, cancellationToken);
            return new { productId, catalogId };
        }, "Product deleted.");

    private static List<MetaProductDto> ReadProducts(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return new List<MetaProductDto>();

        return data.EnumerateArray().Select(MapProduct).ToList();
    }

    private static MetaProductDto MapProduct(JsonElement row) => new()
    {
        Id = MetaGraphResponseHelper.ReadString(row, "id") ?? string.Empty,
        Name = MetaGraphResponseHelper.ReadString(row, "name") ?? string.Empty,
        RetailerId = MetaGraphResponseHelper.ReadString(row, "retailer_id"),
        Price = MetaGraphResponseHelper.ReadString(row, "price"),
        Currency = MetaGraphResponseHelper.ReadString(row, "currency"),
        Availability = MetaGraphResponseHelper.ReadString(row, "availability"),
        ImageUrl = MetaGraphResponseHelper.ReadString(row, "image_url"),
        Url = MetaGraphResponseHelper.ReadString(row, "url"),
        ReviewStatus = MetaGraphResponseHelper.ReadString(row, "review_status")
    };
}
