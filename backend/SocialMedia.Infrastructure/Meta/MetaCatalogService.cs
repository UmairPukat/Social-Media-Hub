using System.Text.Json;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;

namespace SocialMedia.Infrastructure.Meta;

public class MetaCatalogService : IMetaCatalogService
{
    private const string CatalogFields = "id,name,vertical,product_count";
    private const string BusinessCatalogFields =
        $"id,name,owned_product_catalogs{{{CatalogFields}}},client_product_catalogs{{{CatalogFields}}}";

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
            var catalogs = new Dictionary<string, MetaCatalogDto>(StringComparer.Ordinal);

            await TryCollectFromBusinessEdgeAsync(
                userId,
                menuType,
                "me/businesses",
                catalogs,
                cancellationToken,
                ("fields", BusinessCatalogFields),
                ("limit", "50"));

            if (catalogs.Count == 0)
            {
                await TryCollectFromUserAssignedAsync(userId, menuType, catalogs, cancellationToken);
            }

            if (catalogs.Count == 0)
            {
                await TryCollectFromPageBusinessesAsync(userId, menuType, catalogs, cancellationToken);
            }

            if (catalogs.Count == 0)
            {
                await TryCollectFromBusinessListAsync(userId, menuType, catalogs, query, cancellationToken);
            }

            return new MetaPagedResultDto<MetaCatalogDto>
            {
                Items = catalogs.Values.ToList(),
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

    private async Task TryCollectFromBusinessEdgeAsync(
        Guid userId,
        string menuType,
        string path,
        IDictionary<string, MetaCatalogDto> catalogs,
        CancellationToken cancellationToken,
        params (string Key, string Value)[] query)
    {
        try
        {
            using var doc = await _graph.GetAsync(userId, menuType, path, cancellationToken, query);
            if (!doc.RootElement.TryGetProperty("data", out var businesses))
                return;

            foreach (var business in businesses.EnumerateArray())
            {
                var businessId = MetaGraphResponseHelper.ReadString(business, "id");
                AddCatalogNodes(catalogs, business, "owned_product_catalogs", businessId, "owned");
                AddCatalogNodes(catalogs, business, "client_product_catalogs", businessId, "client");
            }
        }
        catch (MetaGraphApiException)
        {
            // Try the next discovery strategy.
        }
    }

    private async Task TryCollectFromUserAssignedAsync(
        Guid userId,
        string menuType,
        IDictionary<string, MetaCatalogDto> catalogs,
        CancellationToken cancellationToken)
    {
        try
        {
            using var meDoc = await _graph.GetAsync(
                userId,
                menuType,
                "me",
                cancellationToken,
                ("fields", "id"));

            var userIdValue = MetaGraphResponseHelper.ReadString(meDoc.RootElement, "id");
            if (string.IsNullOrWhiteSpace(userIdValue))
                return;

            using var assignedDoc = await _graph.GetAsync(
                userId,
                menuType,
                $"{userIdValue}/assigned_product_catalogs",
                cancellationToken,
                ("fields", CatalogFields),
                ("limit", "100"));

            AddCatalogNodes(catalogs, assignedDoc.RootElement, "data", null, "assigned");
        }
        catch (MetaGraphApiException)
        {
            // Try the next discovery strategy.
        }
    }

    private async Task TryCollectFromPageBusinessesAsync(
        Guid userId,
        string menuType,
        IDictionary<string, MetaCatalogDto> catalogs,
        CancellationToken cancellationToken)
    {
        try
        {
            using var pagesDoc = await _graph.GetAsync(
                userId,
                menuType,
                "me/accounts",
                cancellationToken,
                ("fields", $"id,name,business{{id,name,owned_product_catalogs{{{CatalogFields}}}}}" ),
                ("limit", "100"));

            if (!pagesDoc.RootElement.TryGetProperty("data", out var pages))
                return;

            foreach (var page in pages.EnumerateArray())
            {
                if (!page.TryGetProperty("business", out var business) || business.ValueKind != JsonValueKind.Object)
                    continue;

                var businessId = MetaGraphResponseHelper.ReadString(business, "id");
                AddCatalogNodes(catalogs, business, "owned_product_catalogs", businessId, "owned");
            }
        }
        catch (MetaGraphApiException)
        {
            // Try the next discovery strategy.
        }
    }

    private async Task TryCollectFromBusinessListAsync(
        Guid userId,
        string menuType,
        IDictionary<string, MetaCatalogDto> catalogs,
        MetaCatalogListQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            using var businessesDoc = await _graph.GetAsync(
                userId,
                menuType,
                "me/businesses",
                cancellationToken,
                ("fields", "id,name"),
                ("limit", "50"));

            if (!businessesDoc.RootElement.TryGetProperty("data", out var businesses))
                return;

            foreach (var business in businesses.EnumerateArray())
            {
                var businessId = MetaGraphResponseHelper.ReadString(business, "id");
                if (string.IsNullOrWhiteSpace(businessId))
                    continue;

                await TryAddBusinessCatalogEdgeAsync(
                    userId, menuType, catalogs, businessId, "owned_product_catalogs", "owned", query, cancellationToken);
                await TryAddBusinessCatalogEdgeAsync(
                    userId, menuType, catalogs, businessId, "client_product_catalogs", "client", query, cancellationToken);
            }
        }
        catch (MetaGraphApiException)
        {
            // No catalogs available for this user/token.
        }
    }

    private async Task TryAddBusinessCatalogEdgeAsync(
        Guid userId,
        string menuType,
        IDictionary<string, MetaCatalogDto> catalogs,
        string businessId,
        string edge,
        string source,
        MetaCatalogListQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var extra = new List<(string, string)>
            {
                ("fields", CatalogFields),
                ("limit", Math.Clamp(query.Limit, 1, 100).ToString())
            };
            if (!string.IsNullOrWhiteSpace(query.After))
                extra.Add(("after", query.After));

            using var catalogDoc = await _graph.GetAsync(
                userId,
                menuType,
                $"{businessId}/{edge}",
                cancellationToken,
                extra.ToArray());

            AddCatalogNodes(catalogs, catalogDoc.RootElement, "data", businessId, source);
        }
        catch (MetaGraphApiException)
        {
            // Ignore per-business failures and continue.
        }
    }

    private static void AddCatalogNodes(
        IDictionary<string, MetaCatalogDto> catalogs,
        JsonElement container,
        string propertyName,
        string? businessId,
        string source)
    {
        if (!container.TryGetProperty(propertyName, out var data) || data.ValueKind != JsonValueKind.Array)
            return;

        foreach (var row in data.EnumerateArray())
        {
            var id = MetaGraphResponseHelper.ReadString(row, "id");
            if (string.IsNullOrWhiteSpace(id) || catalogs.ContainsKey(id))
                continue;

            catalogs[id] = new MetaCatalogDto
            {
                Id = id,
                Name = MetaGraphResponseHelper.ReadString(row, "name") ?? "Catalog",
                Vertical = MetaGraphResponseHelper.ReadString(row, "vertical"),
                ProductCount = MetaGraphResponseHelper.ReadString(row, "product_count"),
                BusinessId = businessId,
                Source = source
            };
        }
    }

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
