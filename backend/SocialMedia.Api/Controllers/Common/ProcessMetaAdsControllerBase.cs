using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IMetaApiCallTracker _apiCallTracker;

    protected ProcessMetaAdsControllerBase(
        IMetaAdsService adsService,
        IMetaInsightsService insightsService,
        IMetaCatalogService catalogService,
        IMetaApiCallTracker apiCallTracker)
    {
        _adsService = adsService;
        _insightsService = insightsService;
        _catalogService = catalogService;
        _apiCallTracker = apiCallTracker;
    }

    protected abstract string MenuType { get; }

    private IMetaLeadsService Leads => HttpContext.RequestServices.GetRequiredService<IMetaLeadsService>();

    /// <summary>Marketing API health metrics for Advanced Access review. Permission: ads_read.</summary>
    [HttpGet("meta-ads/api-health")]
    public IActionResult GetApiHealth()
    {
        var health = _apiCallTracker.GetHealth(User.GetUserId());
        return Ok(Application.DTOs.Common.ApiResponse<MetaApiHealthDto>.Ok(health, "API health loaded."));
    }

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

    /// <summary>Creates a paused ad whose creative uses an authorized Facebook Page. Permissions: ads_management, pages_manage_ads.</summary>
    [HttpPost("meta-ads/ads")]
    public async Task<IActionResult> CreateAd(
        [FromBody] CreateMetaAdRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _adsService.CreateAdAsync(User.GetUserId(), MenuType, request, cancellationToken);
        return Ok(response);
    }

    /// <summary>Loads a Meta ad preview. Permission: ads_read.</summary>
    [HttpGet("meta-ads/ads/{adId}/preview")]
    public async Task<IActionResult> GetAdPreview(string adId, CancellationToken cancellationToken)
    {
        var response = await _adsService.GetAdPreviewAsync(User.GetUserId(), MenuType, adId, cancellationToken);
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

    /// <summary>Authorized Facebook pages for this module. Tokens stay on the server. Permission: pages_show_list.</summary>
    [HttpGet("meta-ads/pages")]
    public async Task<IActionResult> GetPages(CancellationToken cancellationToken)
    {
        var response = await Leads.GetPagesAsync(User.GetUserId(), MenuType, cancellationToken);
        return Ok(response);
    }

    /// <summary>Lead Ads forms for one authorized page. Permission: leads_retrieval.</summary>
    [HttpGet("meta-ads/pages/{pageId}/leadgen-forms")]
    public async Task<IActionResult> GetLeadForms(
        string pageId,
        [FromQuery] string? after,
        CancellationToken cancellationToken)
    {
        var response = await Leads.GetLeadFormsAsync(User.GetUserId(), MenuType, pageId, after, cancellationToken);
        return Ok(response);
    }

    /// <summary>Whether this page is subscribed to the leadgen webhook field.</summary>
    [HttpGet("meta-ads/pages/{pageId}/leadgen-subscription")]
    public async Task<IActionResult> GetLeadgenSubscription(string pageId, CancellationToken cancellationToken)
    {
        var response = await Leads.GetLeadgenSubscriptionAsync(User.GetUserId(), MenuType, pageId, cancellationToken);
        return Ok(response);
    }

    /// <summary>Subscribes the page to the leadgen webhook field. Permission: pages_manage_metadata, leads_retrieval.</summary>
    [HttpPost("meta-ads/pages/{pageId}/leadgen-subscription")]
    public async Task<IActionResult> SubscribeLeadgen(string pageId, CancellationToken cancellationToken)
    {
        var response = await Leads.SubscribeLeadgenAsync(User.GetUserId(), MenuType, pageId, cancellationToken);
        return Ok(response);
    }

    /// <summary>Removes only the leadgen webhook field. Other page webhook fields stay subscribed.</summary>
    [HttpDelete("meta-ads/pages/{pageId}/leadgen-subscription")]
    public async Task<IActionResult> UnsubscribeLeadgen(string pageId, CancellationToken cancellationToken)
    {
        var response = await Leads.UnsubscribeLeadgenAsync(User.GetUserId(), MenuType, pageId, cancellationToken);
        return Ok(response);
    }

    /// <summary>Retrieves leads for a form and stores them for this user and module. Permission: leads_retrieval.</summary>
    [HttpGet("meta-ads/leadgen-forms/{formId}/leads")]
    public async Task<IActionResult> GetLeads(
        string formId,
        [FromQuery] string pageId,
        [FromQuery] string? formName,
        [FromQuery] string? after,
        CancellationToken cancellationToken)
    {
        var response = await Leads.GetLeadsAsync(
            User.GetUserId(), MenuType, formId, pageId, formName, after, cancellationToken);
        return Ok(response);
    }

    /// <summary>Loads one lead, including every submitted question. Permission: leads_retrieval.</summary>
    [HttpGet("meta-ads/leads/{leadId}")]
    public async Task<IActionResult> GetLead(
        string leadId,
        [FromQuery] string? pageId,
        CancellationToken cancellationToken)
    {
        var response = await Leads.GetLeadAsync(User.GetUserId(), MenuType, leadId, pageId, cancellationToken);
        return Ok(response);
    }

    /// <summary>Updates the local CRM status and notes for a stored lead.</summary>
    [HttpPut("meta-ads/leads/{leadId}")]
    public async Task<IActionResult> UpdateLead(
        string leadId,
        [FromBody] UpdateMetaLeadRequest request,
        CancellationToken cancellationToken)
    {
        var response = await Leads.UpdateLeadAsync(User.GetUserId(), MenuType, leadId, request, cancellationToken);
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
