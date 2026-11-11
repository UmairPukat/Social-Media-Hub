import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';
import { PROCESS_MODULES } from './core/config/process.config';

const processChildren = (connectComponent: () => Promise<{ default?: never } & import('@angular/core').Type<unknown>>) => [
  { path: '', pathMatch: 'full' as const, redirectTo: 'connect' },
  { path: 'connect', loadComponent: connectComponent },
  {
    path: 'create-post',
    loadComponent: () => import('./features/create-post/create-post.component').then(m => m.CreatePostComponent)
  },
  {
    path: 'posts',
    loadComponent: () => import('./features/posts/posts.component').then(m => m.PostsComponent)
  },
  {
    path: 'inbox',
    loadComponent: () => import('./features/inbox/inbox.component').then(m => m.InboxComponent)
  },
  {
    path: 'sync',
    loadComponent: () => import('./features/platform-sync/platform-sync.component').then(m => m.PlatformSyncComponent)
  },
  {
    path: 'analytics',
    loadComponent: () => import('./features/analytics/analytics.component').then(m => m.AnalyticsComponent)
  },
  {
    path: 'accounts',
    loadComponent: () => import('./features/accounts/accounts.component').then(m => m.AccountsComponent)
  },
  {
    path: 'public-pages',
    loadComponent: () =>
      import('./features/public-pages/public-pages.component').then(m => m.PublicPagesComponent)
  }
] satisfies Routes;

function createMetaAdsRoutes(): Routes {
  return [
    {
      path: 'meta-ads',
      children: [
        { path: '', pathMatch: 'full', redirectTo: 'ad-accounts' },
        {
          path: 'ad-accounts',
          loadComponent: () =>
            import('./features/meta-ads/meta-ads-ad-accounts.component').then(m => m.MetaAdsAdAccountsComponent)
        },
        {
          path: 'campaigns',
          loadComponent: () =>
            import('./features/meta-ads/meta-ads-campaigns.component').then(m => m.MetaAdsCampaignsComponent)
        },
        {
          path: 'create-campaign',
          loadComponent: () =>
            import('./features/meta-ads/meta-ads-create-campaign.component').then(m => m.MetaAdsCreateCampaignComponent)
        },
        {
          path: 'ad-sets',
          loadComponent: () =>
            import('./features/meta-ads/meta-ads-ad-sets.component').then(m => m.MetaAdsAdSetsComponent)
        },
        {
          path: 'ads',
          loadComponent: () => import('./features/meta-ads/meta-ads-ads.component').then(m => m.MetaAdsAdsComponent)
        },
        {
          path: 'insights',
          loadComponent: () =>
            import('./features/meta-ads/meta-ads-insights.component').then(m => m.MetaAdsInsightsComponent)
        },
        {
          path: 'catalogs',
          loadComponent: () =>
            import('./features/meta-ads/meta-ads-catalogs.component').then(m => m.MetaAdsCatalogsComponent)
        },
        {
          path: 'products',
          loadComponent: () =>
            import('./features/meta-ads/meta-ads-products.component').then(m => m.MetaAdsProductsComponent)
        }
      ]
    }
  ];
}

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'oauth-complete',
    loadComponent: () =>
      import('./features/oauth-complete/oauth-complete.component').then(m => m.OAuthCompleteComponent)
  },
  {
    path: 'terms',
    loadComponent: () => import('./features/legal/terms/terms.component').then(m => m.TermsComponent)
  },
  {
    path: 'privacy',
    loadComponent: () => import('./features/legal/privacy/privacy.component').then(m => m.PrivacyComponent)
  },
  {
    path: 'app',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell/shell.component').then(m => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'integrations',
        data: { processMenu: PROCESS_MODULES.integrations.id },
        children: [
          ...processChildren(() =>
            import('./features/integrations/integrations.component').then(m => m.IntegrationsComponent)
          ),
          ...createMetaAdsRoutes()
        ]
      },
      {
        path: 'app-connections',
        data: { processMenu: PROCESS_MODULES.appConnections.id },
        children: [
          ...processChildren(() =>
            import('./features/app-connections/app-connections.component').then(m => m.AppConnectionsComponent)
          ),
          ...createMetaAdsRoutes()
        ]
      },
      {
        path: 'developer-apps',
        data: { processMenu: PROCESS_MODULES.developerApps.id },
        children: [
          ...processChildren(() =>
            import('./features/developer-apps/developer-apps.component').then(m => m.DeveloperAppsComponent)
          ),
          ...createMetaAdsRoutes()
        ]
      },
      {
        path: 'social-media-accounts',
        data: { processMenu: PROCESS_MODULES.socialMediaAccounts.id },
        children: [
          ...processChildren(() =>
            import('./features/social-media-accounts/social-media-accounts.component').then(m => m.SocialMediaAccountsComponent)
          ),
          ...createMetaAdsRoutes()
        ]
      },
      {
        path: 'account-connections',
        data: { processMenu: PROCESS_MODULES.accountConnections.id },
        children: [
          ...processChildren(() =>
            import('./features/account-connections/account-connections.component').then(m => m.AccountConnectionsComponent)
          ),
          ...createMetaAdsRoutes()
        ]
      },
      // Legacy redirects
      { path: 'integrations-legacy', redirectTo: 'integrations/connect', pathMatch: 'full' },
      { path: 'create-post', redirectTo: 'integrations/create-post', pathMatch: 'full' },
      { path: 'posts', redirectTo: 'integrations/posts', pathMatch: 'full' },
      { path: 'inbox', redirectTo: 'integrations/inbox', pathMatch: 'full' },
      { path: 'analytics', redirectTo: 'integrations/analytics', pathMatch: 'full' },
      { path: 'accounts', redirectTo: 'integrations/accounts', pathMatch: 'full' },
      {
        path: 'signup',
        loadComponent: () => import('./features/auth/signup/signup.component').then(m => m.SignupComponent)
      },
      {
        path: 'settings',
        loadComponent: () => import('./features/settings/settings.component').then(m => m.SettingsComponent)
      },
      {
        path: 'clear-data',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/clear-data/clear-data.component').then(m => m.ClearDataComponent)
      },
      {
        path: 'users',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/users/users.component').then(m => m.UsersComponent)
      }
    ]
  },
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '**', redirectTo: 'login' }
];
