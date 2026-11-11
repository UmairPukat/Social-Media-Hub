import { Component, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs/operators';
import { processFromRoute } from '../../core/config/process.config';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../core/services/auth.service';
import { ThemeService } from '../../core/services/theme.service';
import { PROCESS_MODULE_LIST } from '../../core/config/process.config';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatIconModule, MatButtonModule],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss'
})
export class ShellComponent {
  readonly auth = inject(AuthService);
  readonly theme = inject(ThemeService);
  private readonly router = inject(Router);
  readonly opened = signal(true);

  readonly processModules = PROCESS_MODULE_LIST;

  readonly globalMenu = [
    { path: '/app/dashboard', icon: 'dashboard', label: 'Dashboard' },
    { path: '/app/users', icon: 'group', label: 'Users', adminOnly: true },
    { path: '/app/signup', icon: 'person_add', label: 'Invite Signup' },
    { path: '/app/settings', icon: 'settings', label: 'Settings' },
    { path: '/app/clear-data', icon: 'delete_forever', label: 'Clear data', adminOnly: true }
  ];

  visibleGlobalMenu() {
    return this.globalMenu.filter((item) => !item.adminOnly || this.auth.isAdmin());
  }

  readonly processSubItems = [
    { suffix: 'connect', icon: 'hub', label: 'Connect' },
    { suffix: 'create-post', icon: 'post_add', label: 'Create Post' },
    { suffix: 'posts', icon: 'dynamic_feed', label: 'Posts' },
    { suffix: 'inbox', icon: 'inbox', label: 'Inbox' },
    { suffix: 'sync', icon: 'sync', label: 'Platform sync' },
    { suffix: 'analytics', icon: 'insights', label: 'Analytics' },
    { suffix: 'accounts', icon: 'link', label: 'Connected Accounts' },
    { suffix: 'public-pages', icon: 'travel_explore', label: 'Public Pages' }
  ];

  readonly metaAdsNavItems = [
    { suffix: 'meta-ads/ad-accounts', icon: 'account_balance', label: 'Ad Accounts' },
    { suffix: 'meta-ads/campaigns', icon: 'campaign', label: 'Campaigns' },
    { suffix: 'meta-ads/create-campaign', icon: 'add_circle', label: 'Create Campaign' },
    { suffix: 'meta-ads/ad-sets', icon: 'layers', label: 'Ad Sets' },
    { suffix: 'meta-ads/ads', icon: 'ads_click', label: 'Ads' },
    { suffix: 'meta-ads/insights', icon: 'insights', label: 'Insights' },
    { suffix: 'meta-ads/catalogs', icon: 'inventory_2', label: 'Catalogs' },
    { suffix: 'meta-ads/products', icon: 'shopping_bag', label: 'Products' }
  ];

  expanded = signal<Record<string, boolean>>({
    integration: true,
    integration_meta_ads: true,
    app_connection: false,
    app_connection_meta_ads: false,
    developer_app: false,
    developer_app_meta_ads: false,
    social_media_account: false,
    social_media_account_meta_ads: false,
    account_connection: false,
    account_connection_meta_ads: false
  });

  constructor() {
    this.syncExpanded(this.router.url);
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd)
    ).subscribe(event => this.syncExpanded(event.urlAfterRedirects));
  }

  metaAdsGroupKey(moduleId: string): string {
    return `${moduleId}_meta_ads`;
  }

  private syncExpanded(url: string): void {
    const module = processFromRoute(url);
    if (!module) return;
    this.expanded.update(map => ({
      ...map,
      [module.id]: true,
      [this.metaAdsGroupKey(module.id)]: url.includes('/meta-ads')
        ? true
        : map[this.metaAdsGroupKey(module.id)]
    }));
  }

  toggle(): void {
    this.opened.update(v => !v);
  }

  toggleGroup(menuType: string): void {
    this.expanded.update(map => ({ ...map, [menuType]: !map[menuType] }));
  }

  isExpanded(menuType: string): boolean {
    return !!this.expanded()[menuType];
  }

  logout(): void {
    this.auth.logout();
  }
}
