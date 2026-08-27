import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

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
    path: 'analytics',
    loadComponent: () => import('./features/analytics/analytics.component').then(m => m.AnalyticsComponent)
  },
  {
    path: 'accounts',
    loadComponent: () => import('./features/accounts/accounts.component').then(m => m.AccountsComponent)
  }
] satisfies Routes;

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent)
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
        children: processChildren(() =>
          import('./features/integrations/integrations.component').then(m => m.IntegrationsComponent)
        )
      },
      {
        path: 'app-connections',
        children: processChildren(() =>
          import('./features/app-connections/app-connections.component').then(m => m.AppConnectionsComponent)
        )
      },
      {
        path: 'developer-apps',
        children: processChildren(() =>
          import('./features/developer-apps/developer-apps.component').then(m => m.DeveloperAppsComponent)
        )
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
      }
    ]
  },
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '**', redirectTo: 'login' }
];
