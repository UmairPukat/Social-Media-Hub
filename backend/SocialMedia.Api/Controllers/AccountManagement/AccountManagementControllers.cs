using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Controllers.Common;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers.AccountManagement;

[Authorize]
[Route(ProcessModules.AccountManagement.ApiRoute)]
[ApiController]
public class ConnectionController : ProcessConnectionControllerBase
{
    public ConnectionController(
        IIntegrationService integrationService,
        IProcessAppConfigService configService,
        IPublicPagesService publicPages)
        : base(integrationService, configService, publicPages) { }

    protected override string MenuType => ProcessModules.AccountManagement.MenuType;
}

[Authorize]
[Route(ProcessModules.AccountManagement.ApiRoute)]
[ApiController]
public class PostsController : ProcessPostsControllerBase
{
    public PostsController(IPostService postService, IYouTubeSyncService youTubeSync, ITikTokSyncService tikTokSync)
        : base(postService, youTubeSync, tikTokSync) { }

    protected override string MenuType => ProcessModules.AccountManagement.MenuType;
}

[Authorize]
[Route(ProcessModules.AccountManagement.ApiRoute)]
[ApiController]
public class YouTubeSyncController : ProcessYouTubeSyncControllerBase
{
    public YouTubeSyncController(IYouTubeSyncService youTubeSync) : base(youTubeSync) { }

    protected override string MenuType => ProcessModules.AccountManagement.MenuType;
}

[Authorize]
[Route(ProcessModules.AccountManagement.ApiRoute)]
[ApiController]
public class TikTokSyncController : ProcessTikTokSyncControllerBase
{
    public TikTokSyncController(ITikTokSyncService tikTokSync) : base(tikTokSync) { }

    protected override string MenuType => ProcessModules.AccountManagement.MenuType;
}

[Authorize]
[Route(ProcessModules.AccountManagement.ApiRoute)]
[ApiController]
public class InboxController : ProcessInboxControllerBase
{
    public InboxController(IInboxService inboxService) : base(inboxService) { }

    protected override string MenuType => ProcessModules.AccountManagement.MenuType;
}

[AllowAnonymous]
[Route(ProcessModules.AccountManagement.ApiRoute)]
[ApiController]
public class WebhooksController : ProcessWebhooksControllerBase
{
    public WebhooksController(IWebhookService webhookService, ILoggerFactory loggerFactory)
        : base(webhookService, loggerFactory) { }

    protected override string MenuType => ProcessModules.AccountManagement.MenuType;
    protected override string WebhookRoute => ProcessModules.AccountManagement.WebhookRoute;
}

[AllowAnonymous]
[Route(ProcessModules.AccountManagement.ApiRoute + "/whatsapp")]
[ApiController]
public class WhatsAppWebhooksController : ProcessWebhooksControllerBase
{
    public WhatsAppWebhooksController(IWebhookService webhookService, ILoggerFactory loggerFactory)
        : base(webhookService, loggerFactory) { }

    protected override string MenuType => ProcessModules.AccountManagement.MenuType;
    protected override string WebhookRoute => ProcessModules.AccountManagement.WhatsAppWebhookRoute;
}

[Authorize]
[Route(ProcessModules.AccountManagement.ApiRoute)]
[ApiController]
public class AnalyticsController : ProcessAnalyticsControllerBase
{
    public AnalyticsController(IDashboardService dashboardService) : base(dashboardService) { }

    protected override string MenuType => ProcessModules.AccountManagement.MenuType;
}

[Authorize]
[Route(ProcessModules.AccountManagement.ApiRoute)]
[ApiController]
public class MetaAdsController : ProcessMetaAdsControllerBase
{
    public MetaAdsController(
        IMetaAdsService adsService,
        IMetaInsightsService insightsService,
        IMetaCatalogService catalogService,
        IMetaApiCallTracker apiCallTracker)
        : base(adsService, insightsService, catalogService, apiCallTracker) { }

    protected override string MenuType => ProcessModules.AccountManagement.MenuType;
}
