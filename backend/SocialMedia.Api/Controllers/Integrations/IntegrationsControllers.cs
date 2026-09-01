using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Controllers.Common;
using SocialMedia.Application.Catalog;
using SocialMedia.Application.Interfaces;

namespace SocialMedia.Api.Controllers.Integrations;

[Authorize]
[Route(ProcessModules.Integrations.ApiRoute)]
[ApiController]
public class ConnectionController : ProcessConnectionControllerBase
{
    public ConnectionController(IIntegrationService integrationService, IProcessAppConfigService configService)
        : base(integrationService, configService) { }

    protected override string MenuType => ProcessModules.Integrations.MenuType;
}

[Authorize]
[Route(ProcessModules.Integrations.ApiRoute)]
[ApiController]
public class PostsController : ProcessPostsControllerBase
{
    public PostsController(IPostService postService, IYouTubeSyncService youTubeSync)
        : base(postService, youTubeSync) { }

    protected override string MenuType => ProcessModules.Integrations.MenuType;
}

[Authorize]
[Route(ProcessModules.Integrations.ApiRoute)]
[ApiController]
public class YouTubeSyncController : ProcessYouTubeSyncControllerBase
{
    public YouTubeSyncController(IYouTubeSyncService youTubeSync) : base(youTubeSync) { }

    protected override string MenuType => ProcessModules.Integrations.MenuType;
}

[Authorize]
[Route(ProcessModules.Integrations.ApiRoute)]
[ApiController]
public class InboxController : ProcessInboxControllerBase
{
    public InboxController(IInboxService inboxService) : base(inboxService) { }

    protected override string MenuType => ProcessModules.Integrations.MenuType;
}

[AllowAnonymous]
[Route(ProcessModules.Integrations.ApiRoute)]
[ApiController]
public class WebhooksController : ProcessWebhooksControllerBase
{
    public WebhooksController(IWebhookService webhookService, ILoggerFactory loggerFactory)
        : base(webhookService, loggerFactory) { }

    protected override string MenuType => ProcessModules.Integrations.MenuType;
    protected override string WebhookRoute => ProcessModules.Integrations.WebhookRoute;
}

[Authorize]
[Route(ProcessModules.Integrations.ApiRoute)]
[ApiController]
public class AnalyticsController : ProcessAnalyticsControllerBase
{
    public AnalyticsController(IDashboardService dashboardService) : base(dashboardService) { }

    protected override string MenuType => ProcessModules.Integrations.MenuType;
}
