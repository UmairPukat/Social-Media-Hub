using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Extensions;
using SocialMedia.Application.DTOs.Meta;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers.Common;

[Authorize]
[ApiController]
public abstract class ProcessMetaAdsControllerBase : ControllerBase
{
    private readonly IMetaAdsService _adsService;
    private readonly IMetaInsightsService _insightsService;
    private readonly IMetaCatalogService _catalogService;

    protected ProcessMetaAdsControllerBase(
        IMetaAdsService adsService,
        IMetaInsightsService insightsService,
        IMetaCatalogService catalogService)
    {
        _adsService = adsService;
        _insightsService = insightsService;
        _catalogService = catalogService;
    }

    protected abstract string MenuType { get; }

    /// <summary>Lists ad accounts for the connected Meta user. Permission: ads_read.</summary>
    [HttpGet("meta-ads/ad-accounts")]
    public async Task<IActionResult> GetAdAccounts(CancellationToken cancellationToken)
    {
        var response = await _adsService.GetAdAccountsAsync(User.GetUserId(), MenuType, cancellationToken);
        return Ok(response);
    }

    /// <summary>Lists campaigns for an ad account. Permission: ads_read.</summary>
    [HttpGet("meta-ads/campaigns")]
    public async Task<IActionResult> GetCampaigns([FromQuery] MetaListQuery query, CancellationToken cancellationToken)
    {
        var response = await _adsService.GetCampaignsAsync(User.GetUserId(), MenuType, query, cancellationToken);
        return Ok(response);
    }

    /// <summary>Creates a paused campaign for App Review demos. Permission: ads_management.</summary>
    [HttpPost("meta-ads/campaigns")]
    public async Task<IActionResult> CreateCampaign(
        [FromBody] CreateMetaCampaignRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _adsService.CreateCampaignAsync(User.GetUserId(), MenuType, request, cancellationToken);
        return Ok(response);
    }

    /// <summary>Updates campaign name or status. Permission: ads_management.</summary>
    [HttpPut("meta-ads/campaigns/{campaignId}")]
    public async Task<IActionResult> UpdateCampaign(
        string campaignId,
        [FromBody] UpdateMetaCampaignRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _adsService.UpdateCampaignAsync(User.GetUserId(), MenuType, campaignId, request, cancellationToken);
        return Ok(response);
    }

    /// <summary>Lists ad sets. Permission: ads_read.</summary>
    [HttpGet("meta-ads/adsets")]
    public async Task<IActionResult> GetAdSets([FromQuery] MetaListQuery query, CancellationToken cancellationToken)
    {
        var response = await _adsService.GetAdSetsAsync(User.GetUserId(), MenuType, query, cancellationToken);
        return Ok(response);
    }

    /// <summary>Creates a paused ad set. Permission: ads_management.</summary>
    [HttpPost("meta-ads/adsets")]
    public async Task<IActionResult> CreateAdSet(
        [FromBody] CreateMetaAdSetRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _adsService.CreateAdSetAsync(User.GetUserId(), MenuType, request, cancellationToken);
        return Ok(response);
    }

    /// <summary>Updates ad set fields. Permission: ads_management.</summary>
    [HttpPut("meta-ads/adsets/{adSetId}")]
    public async Task<IActionResult> UpdateAdSet(
        string adSetId,
        [FromBody] UpdateMetaAdSetRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _adsService.UpdateAdSetAsync(User.GetUserId(), MenuType, adSetId, request, cancellationToken);
        return Ok(response);
    }

    /// <summary>Lists ads. Permission: ads_read.</summary>
    [HttpGet("meta-ads/ads")]
    public async Task<IActionResult> GetAds([FromQuery] MetaListQuery query, CancellationToken cancellationToken)
    {
        var response = await _adsService.GetAdsAsync(User.GetUserId(), MenuType, query, cancellationToken);
        return Ok(response);
    }

    /// <summary>Updates ad name or status. Permission: ads_management.</summary>
    [HttpPut("meta-ads/ads/{adId}")]
    public async Task<IActionResult> UpdateAd(
        string adId,
        [FromBody] UpdateMetaAdRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _adsService.UpdateAdAsync(User.GetUserId(), MenuType, adId, request, cancellationToken);
        return Ok(response);
    }

    /// <summary>Reads insights for a campaign, ad set, or ad. Permission: read_insights.</summary>
    [HttpGet("meta-ads/insights")]
    public async Task<IActionResult> GetInsights([FromQuery] MetaInsightsQuery query, CancellationToken cancellationToken)
    {
        var response = await _insightsService.GetInsightsAsync(User.GetUserId(), MenuType, query, cancellationToken);
        return Ok(response);
    }

    /// <summary>Lists product catalogs. Permission: catalog_management.</summary>
    [HttpGet("meta-ads/catalogs")]
    public async Task<IActionResult> GetCatalogs([FromQuery] MetaCatalogListQuery query, CancellationToken cancellationToken)
    {
        var response = await _catalogService.GetCatalogsAsync(User.GetUserId(), MenuType, query, cancellationToken);
        return Ok(response);
    }

    /// <summary>Creates a product catalog on a Meta business. Permission: catalog_management.</summary>
    [HttpPost("meta-ads/catalogs")]
    public async Task<IActionResult> CreateCatalog(
        [FromBody] CreateMetaCatalogRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _catalogService.CreateCatalogAsync(User.GetUserId(), MenuType, request, cancellationToken);
        return Ok(response);
    }

    /// <summary>Lists products in a catalog. Permission: catalog_management.</summary>
    [HttpGet("meta-ads/catalogs/{catalogId}/products")]
    public async Task<IActionResult> GetProducts(
        string catalogId,
        [FromQuery] MetaProductListQuery query,
        CancellationToken cancellationToken)
    {
        query.CatalogId = catalogId;
        var response = await _catalogService.GetProductsAsync(User.GetUserId(), MenuType, query, cancellationToken);
        return Ok(response);
    }

    /// <summary>Creates a catalog product. Permission: catalog_management.</summary>
    [HttpPost("meta-ads/catalogs/{catalogId}/products")]
    public async Task<IActionResult> CreateProduct(
        string catalogId,
        [FromBody] CreateMetaProductRequest request,
        CancellationToken cancellationToken)
    {
        request.CatalogId = catalogId;
        var response = await _catalogService.CreateProductAsync(User.GetUserId(), MenuType, request, cancellationToken);
        return Ok(response);
    }

    /// <summary>Updates a catalog product. Permission: catalog_management.</summary>
    [HttpPut("meta-ads/catalogs/{catalogId}/products/{productId}")]
    public async Task<IActionResult> UpdateProduct(
        string catalogId,
        string productId,
        [FromBody] UpdateMetaProductRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _catalogService.UpdateProductAsync(
            User.GetUserId(), MenuType, catalogId, productId, request, cancellationToken);
        return Ok(response);
    }

    /// <summary>Deletes a catalog product. Permission: catalog_management.</summary>
    [HttpDelete("meta-ads/catalogs/{catalogId}/products/{productId}")]
    public async Task<IActionResult> DeleteProduct(
        string catalogId,
        string productId,
        CancellationToken cancellationToken)
    {
        var response = await _catalogService.DeleteProductAsync(
            User.GetUserId(), MenuType, catalogId, productId, cancellationToken);
        return Ok(response);
    }
}
