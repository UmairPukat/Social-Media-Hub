import { Routes } from '@angular/router';
import { authGuard, publicGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  {
    path: 'login',
    canActivate: [publicGuard],
    loadComponent: () => import('./features/auth/login/login.component').then((m) => m.LoginComponent)
  },
  {
    path: 'signup',
    canActivate: [authGuard],
    loadComponent: () => import('./features/auth/signup/signup.component').then((m) => m.SignupComponent)
  },
  {
    path: 'oauth/:platform/callback',
    canActivate: [authGuard],
    loadComponent: () => import('./features/oauth/oauth-callback.component').then((m) => m.OauthCallbackComponent)
  },
  {
    path: 'app',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell/shell.component').then((m) => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent)
      },
      {
        path: 'integrations',
        loadComponent: () => import('./features/integrations/integrations.component').then((m) => m.IntegrationsComponent)
      },
      {
        path: 'create-post',
        loadComponent: () => import('./features/create-post/create-post.component').then((m) => m.CreatePostComponent)
      },
      {
        path: 'inbox',
        loadComponent: () => import('./features/inbox/inbox.component').then((m) => m.InboxComponent)
      },
      {
        path: 'posts',
        loadComponent: () => import('./features/posts/posts.component').then((m) => m.PostsComponent)
      },
      {
        path: 'analytics',
        loadComponent: () => import('./features/analytics/analytics.component').then((m) => m.AnalyticsComponent)
      },
      {
        path: 'accounts',
        loadComponent: () => import('./features/accounts/accounts.component').then((m) => m.AccountsComponent)
      },
      {
        path: 'settings',
        loadComponent: () => import('./features/settings/settings.component').then((m) => m.SettingsComponent)
      },
      {
        path: 'signup',
        loadComponent: () => import('./features/auth/signup/signup.component').then((m) => m.SignupComponent)
      }
    ]
  },
  { path: '**', redirectTo: 'login' }
];
