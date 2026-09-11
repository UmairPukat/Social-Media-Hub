using System.Text.Json;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Meta;

namespace SocialMedia.Infrastructure.Meta;

public class MetaCatalogService : IMetaCatalogService
{
    private const string CatalogFields = "id,name,vertical,product_count";
    private const string BusinessCatalogFields =
        $"id,name,owned_product_catalogs{{{CatalogFields}}},client_product_catalogs{{{CatalogFields}}},product_catalogs{{{CatalogFields}}}";

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
            var discovery = new CatalogDiscovery();
            var businessIds = new HashSet<string>(StringComparer.Ordinal);

            await TryCollectFromBusinessEdgeAsync(
                userId, menuType, discovery, businessIds, cancellationToken,
                "me/businesses",
                ("fields", BusinessCatalogFields),
                ("limit", "50"));

            await TryCollectFromBusinessUsersAsync(userId, menuType, discovery, businessIds, cancellationToken);
            await TryCollectFromAdAccountBusinessesAsync(userId, menuType, discovery, businessIds, cancellationToken);
            await TryCollectFromPageBusinessesAsync(userId, menuType, discovery, businessIds, cancellationToken);

            foreach (var businessId in businessIds)
            {
                await TryAddBusinessCatalogEdgeAsync(
                    userId, menuType, discovery, businessId, "owned_product_catalogs", "owned", query, cancellationToken);
                await TryAddBusinessCatalogEdgeAsync(
                    userId, menuType, discovery, businessId, "client_product_catalogs", "client", query, cancellationToken);
                await TryAddBusinessCatalogEdgeAsync(
                    userId, menuType, discovery, businessId, "product_catalogs", "product", query, cancellationToken);
            }

            if (discovery.Catalogs.Count == 0 && discovery.PermissionDenied)
            {
                throw new MetaGraphApiException(
                    "Meta denied catalog access. Reconnect Facebook in Connect and grant catalog_management + business_management.");
            }

            return new MetaPagedResultDto<MetaCatalogDto>
            {
                Items = discovery.Catalogs.Values.ToList(),
                NextCursor = null
            };
        }, "Catalogs loaded.");

    public Task<Application.DTOs.Common.ApiResponse<MetaCatalogDto>> CreateCatalogAsync(
        Guid userId,
        string menuType,
        CreateMetaCatalogRequest request,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            var businessId = request.BusinessId?.Trim();
            if (string.IsNullOrWhiteSpace(businessId))
                businessId = await ResolveFirstBusinessIdAsync(userId, menuType, cancellationToken);

            if (string.IsNullOrWhiteSpace(businessId))
                throw new MetaGraphApiException("No Meta Business found. Create a Business in Meta Business Manager first.");

            var payload = new Dictionary<string, string>
            {
                ["name"] = request.Name.Trim(),
                ["vertical"] = string.IsNullOrWhiteSpace(request.Vertical) ? "commerce" : request.Vertical.Trim()
            };

            using var doc = await _graph.PostFormAsync(
                userId,
                menuType,
                $"{businessId}/owned_product_catalogs",
                payload,
                cancellationToken);

            var id = MetaGraphResponseHelper.ReadString(doc.RootElement, "id")
                ?? throw new MetaGraphApiException("Meta did not return a catalog id.");

            await EnsureCatalogManageAccessAsync(userId, menuType, id, businessId, cancellationToken);

            using var loaded = await _graph.GetAsync(
                userId,
                menuType,
                id,
                cancellationToken,
                ("fields", CatalogFields));

            return new MetaCatalogDto
            {
                Id = id,
                Name = MetaGraphResponseHelper.ReadString(loaded.RootElement, "name") ?? request.Name.Trim(),
                Vertical = MetaGraphResponseHelper.ReadString(loaded.RootElement, "vertical"),
                ProductCount = MetaGraphResponseHelper.ReadString(loaded.RootElement, "product_count"),
                BusinessId = businessId,
                Source = "owned"
            };
        }, "Catalog created.");

    public Task<Application.DTOs.Common.ApiResponse<MetaPagedResultDto<MetaProductDto>>> GetProductsAsync(
        Guid userId,
        string menuType,
        MetaProductListQuery query,
        CancellationToken cancellationToken = default)
        => MetaApiExecutor.RunAsync(async () =>
        {
            var catalogId = query.CatalogId.Trim();
            var limit = Math.Clamp(query.Limit, 1, 100).ToString();
            var extra = new List<(string, string)>
            {
                ("fields", "id,name,retailer_id,price,currency,availability,image_url,url,review_status"),
                ("limit", limit)
            };
            if (!string.IsNullOrWhiteSpace(query.After))
                extra.Add(("after", query.After));

            using var doc = await GetCatalogProductsAsync(
                userId,
                menuType,
                catalogId,
                query.BusinessId,
                extra,
                cancellationToken);

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
            var catalogId = request.CatalogId.Trim();
            await EnsureCatalogManageAccessAsync(
                userId, menuType, catalogId, request.BusinessId, cancellationToken);

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

            using var doc = await PostCatalogProductsAsync(
                userId,
                menuType,
                catalogId,
                request.BusinessId,
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
            await EnsureCatalogManageAccessAsync(userId, menuType, catalogId, null, cancellationToken);

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
            await EnsureCatalogManageAccessAsync(userId, menuType, catalogId, null, cancellationToken);
            await _graph.DeleteAsync(userId, menuType, productId, cancellationToken);
            return new { productId, catalogId };
        }, "Product deleted.");

    private async Task<JsonDocument> GetCatalogProductsAsync(
        Guid userId,
        string menuType,
        string catalogId,
        string? businessId,
        List<(string Key, string Value)> query,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _graph.GetAsync(
                userId,
                menuType,
                $"{catalogId}/products",
                cancellationToken,
                query.ToArray());
        }
        catch (MetaGraphApiException ex) when (IsCatalogPermissionError(ex))
        {
            await EnsureCatalogManageAccessAsync(userId, menuType, catalogId, businessId, cancellationToken);
            return await _graph.GetAsync(
                userId,
                menuType,
                $"{catalogId}/products",
                cancellationToken,
                query.ToArray());
        }
    }

    private async Task<JsonDocument> PostCatalogProductsAsync(
        Guid userId,
        string menuType,
        string catalogId,
        string? businessId,
        IDictionary<string, string> payload,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _graph.PostFormAsync(
                userId,
                menuType,
                $"{catalogId}/products",
                payload,
                cancellationToken);
        }
        catch (MetaGraphApiException ex) when (IsCatalogPermissionError(ex))
        {
            await EnsureCatalogManageAccessAsync(userId, menuType, catalogId, businessId, cancellationToken);
            return await _graph.PostFormAsync(
                userId,
                menuType,
                $"{catalogId}/products",
                payload,
                cancellationToken);
        }
    }

    private async Task EnsureCatalogManageAccessAsync(
        Guid userId,
        string menuType,
        string catalogId,
        string? businessId,
        CancellationToken cancellationToken)
    {
        businessId = string.IsNullOrWhiteSpace(businessId)
            ? await ResolveBusinessIdForCatalogAsync(userId, menuType, catalogId, cancellationToken)
            : businessId.Trim();

        if (string.IsNullOrWhiteSpace(businessId))
            return;

        var businessUserId = await ResolveBusinessUserIdAsync(userId, menuType, businessId, cancellationToken);
        if (string.IsNullOrWhiteSpace(businessUserId))
            return;

        try
        {
            await _graph.PostFormAsync(
                userId,
                menuType,
                $"{catalogId.Trim()}/assigned_users",
                new Dictionary<string, string>
                {
                    ["user"] = businessUserId,
                    ["business"] = businessId,
                    ["tasks"] = "[\"MANAGE\",\"ADVERTISE\"]"
                },
                cancellationToken);
        }
        catch (MetaGraphApiException)
        {
            // User may already have MANAGE or may not be a business admin.
        }
    }

    private async Task<string?> ResolveBusinessIdForCatalogAsync(
        Guid userId,
        string menuType,
        string catalogId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var doc = await _graph.GetAsync(
                userId,
                menuType,
                catalogId.Trim(),
                cancellationToken,
                ("fields", "business{id}"));

            if (doc.RootElement.TryGetProperty("business", out var business) &&
                business.ValueKind == JsonValueKind.Object)
            {
                return MetaGraphResponseHelper.ReadString(business, "id");
            }
        }
        catch (MetaGraphApiException)
        {
            // Fall through.
        }

        return null;
    }

    private async Task<string?> ResolveBusinessUserIdAsync(
        Guid userId,
        string menuType,
        string businessId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var doc = await _graph.GetAsync(
                userId,
                menuType,
                "me/business_users",
                cancellationToken,
                ("fields", "id,business{id}"),
                ("limit", "100"));

            if (!doc.RootElement.TryGetProperty("data", out var rows))
                return null;

            foreach (var row in rows.EnumerateArray())
            {
                if (!row.TryGetProperty("business", out var business) || business.ValueKind != JsonValueKind.Object)
                    continue;

                var id = MetaGraphResponseHelper.ReadString(business, "id");
                if (!string.Equals(id, businessId, StringComparison.Ordinal))
                    continue;

                return MetaGraphResponseHelper.ReadString(row, "id");
            }
        }
        catch (MetaGraphApiException)
        {
            // Fall through.
        }

        return null;
    }

    private static bool IsCatalogPermissionError(MetaGraphApiException ex) =>
        ex.MetaErrorCode == "200" || ex.HttpStatusCode == 403;

    private async Task<string?> ResolveFirstBusinessIdAsync(
        Guid userId,
        string menuType,
        CancellationToken cancellationToken)
    {
        var businessIds = new HashSet<string>(StringComparer.Ordinal);
        var discovery = new CatalogDiscovery();

        await TryCollectFromBusinessEdgeAsync(
            userId, menuType, discovery, businessIds, cancellationToken,
            "me/businesses",
            ("fields", "id"),
            ("limit", "50"));
        await TryCollectFromAdAccountBusinessesAsync(userId, menuType, discovery, businessIds, cancellationToken);

        return businessIds.FirstOrDefault();
    }

    private async Task TryCollectFromBusinessEdgeAsync(
        Guid userId,
        string menuType,
        CatalogDiscovery discovery,
        ISet<string> businessIds,
        CancellationToken cancellationToken,
        string path,
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
                if (!string.IsNullOrWhiteSpace(businessId))
                    businessIds.Add(businessId);

                AddCatalogNodes(discovery.Catalogs, business, "owned_product_catalogs", businessId, "owned");
                AddCatalogNodes(discovery.Catalogs, business, "client_product_catalogs", businessId, "client");
                AddCatalogNodes(discovery.Catalogs, business, "product_catalogs", businessId, "product");
            }
        }
        catch (MetaGraphApiException ex)
        {
            discovery.NoteFailure(ex);
        }
    }

    private async Task TryCollectFromBusinessUsersAsync(
        Guid userId,
        string menuType,
        CatalogDiscovery discovery,
        ISet<string> businessIds,
        CancellationToken cancellationToken)
    {
        try
        {
            using var businessUsersDoc = await _graph.GetAsync(
                userId,
                menuType,
                "me/business_users",
                cancellationToken,
                ("fields", "id,business{id,name}"),
                ("limit", "100"));

            if (!businessUsersDoc.RootElement.TryGetProperty("data", out var businessUsers))
                return;

            foreach (var businessUser in businessUsers.EnumerateArray())
            {
                var businessUserId = MetaGraphResponseHelper.ReadString(businessUser, "id");
                if (businessUser.TryGetProperty("business", out var business) && business.ValueKind == JsonValueKind.Object)
                {
                    var businessId = MetaGraphResponseHelper.ReadString(business, "id");
                    if (!string.IsNullOrWhiteSpace(businessId))
                        businessIds.Add(businessId);
                }

                if (string.IsNullOrWhiteSpace(businessUserId))
                    continue;

                try
                {
                    using var assignedDoc = await _graph.GetAsync(
                        userId,
                        menuType,
                        $"{businessUserId}/assigned_product_catalogs",
                        cancellationToken,
                        ("fields", CatalogFields),
                        ("limit", "100"));

                    string? assignedBusinessId = null;
                    if (businessUser.TryGetProperty("business", out var businessObj) &&
                        businessObj.ValueKind == JsonValueKind.Object)
                    {
                        assignedBusinessId = MetaGraphResponseHelper.ReadString(businessObj, "id");
                    }

                    AddCatalogNodes(
                        discovery.Catalogs,
                        assignedDoc.RootElement,
                        "data",
                        assignedBusinessId,
                        "assigned");
                }
                catch (MetaGraphApiException ex)
                {
                    discovery.NoteFailure(ex);
                }
            }
        }
        catch (MetaGraphApiException ex)
        {
            discovery.NoteFailure(ex);
        }
    }

    private async Task TryCollectFromAdAccountBusinessesAsync(
        Guid userId,
        string menuType,
        CatalogDiscovery discovery,
        ISet<string> businessIds,
        CancellationToken cancellationToken)
    {
        try
        {
            using var adAccountsDoc = await _graph.GetAsync(
                userId,
                menuType,
                "me/adaccounts",
                cancellationToken,
                ("fields", "id,name,business{id,name}"),
                ("limit", "100"));

            if (!adAccountsDoc.RootElement.TryGetProperty("data", out var adAccounts))
                return;

            foreach (var adAccount in adAccounts.EnumerateArray())
            {
                if (!adAccount.TryGetProperty("business", out var business) || business.ValueKind != JsonValueKind.Object)
                    continue;

                var businessId = MetaGraphResponseHelper.ReadString(business, "id");
                if (string.IsNullOrWhiteSpace(businessId))
                    continue;

                businessIds.Add(businessId);
                AddCatalogNodes(discovery.Catalogs, business, "owned_product_catalogs", businessId, "owned");
            }
        }
        catch (MetaGraphApiException ex)
        {
            discovery.NoteFailure(ex);
        }
    }

    private async Task TryCollectFromPageBusinessesAsync(
        Guid userId,
        string menuType,
        CatalogDiscovery discovery,
        ISet<string> businessIds,
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
                if (!string.IsNullOrWhiteSpace(businessId))
                    businessIds.Add(businessId);

                AddCatalogNodes(discovery.Catalogs, business, "owned_product_catalogs", businessId, "owned");
            }
        }
        catch (MetaGraphApiException ex)
        {
            discovery.NoteFailure(ex);
        }
    }

    private async Task TryAddBusinessCatalogEdgeAsync(
        Guid userId,
        string menuType,
        CatalogDiscovery discovery,
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

            AddCatalogNodes(discovery.Catalogs, catalogDoc.RootElement, "data", businessId, source);
        }
        catch (MetaGraphApiException ex)
        {
            discovery.NoteFailure(ex);
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

    private sealed class CatalogDiscovery
    {
        public Dictionary<string, MetaCatalogDto> Catalogs { get; } = new(StringComparer.Ordinal);
        public bool PermissionDenied { get; private set; }

        public void NoteFailure(MetaGraphApiException ex)
        {
            if (ex.MetaErrorCode is "200" or "10" or "190")
                PermissionDenied = true;
        }
    }
}
