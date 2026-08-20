import { Component, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { filter } from 'rxjs/operators';
import { AuthService } from '../../core/services/auth.service';
import { ThemeService } from '../../core/services/theme.service';

interface NavChild {
  path: string;
  icon: string;
  label: string;
}

interface NavItem {
  path?: string;
  icon: string;
  label: string;
  children?: NavChild[];
}

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatIconModule,
    MatButtonModule
  ],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss'
})
export class ShellComponent {
  readonly auth = inject(AuthService);
  readonly theme = inject(ThemeService);
  private readonly router = inject(Router);

  readonly opened = signal(true);
  readonly expandedGroups = signal<Record<string, boolean>>({
    'environment-variables': true
  });

  readonly menu: NavItem[] = [
    { path: '/app/dashboard', icon: 'dashboard', label: 'Dashboard' },
    { path: '/app/integrations', icon: 'hub', label: 'Integrations' },
    { path: '/app/create-post', icon: 'post_add', label: 'Create Post' },
    { path: '/app/posts', icon: 'dynamic_feed', label: 'Posts' },
    { path: '/app/inbox', icon: 'inbox', label: 'Inbox' },
    { path: '/app/analytics', icon: 'insights', label: 'Analytics' },
    { path: '/app/accounts', icon: 'link', label: 'Connected Accounts' },
    { path: '/app/signup', icon: 'person_add', label: 'Invite Signup' },
    {
      icon: 'tune',
      label: 'Environment Variables',
      children: [
        { path: '/app/environment-variables/frontend', icon: 'web', label: 'Frontend' },
        { path: '/app/environment-variables/backend', icon: 'dns', label: 'Backend' }
      ]
    },
    { path: '/app/settings', icon: 'settings', label: 'Settings' }
  ];

  readonly activeGroupIds = computed(() => {
    const url = this.currentUrl();
    const active = new Set<string>();
    for (const item of this.menu) {
      if (item.children?.some((child) => url.startsWith(child.path))) {
        active.add(this.groupId(item));
      }
    }
    return active;
  });

  private readonly currentUrl = signal(this.router.url);

  constructor() {
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => this.currentUrl.set(event.urlAfterRedirects));
  }

  toggle(): void {
    this.opened.update(v => !v);
  }

  logout(): void {
    this.auth.logout();
  }

  groupId(item: NavItem): string {
    return item.label.toLowerCase().replace(/\s+/g, '-');
  }

  isGroupExpanded(item: NavItem): boolean {
    const id = this.groupId(item);
    return this.expandedGroups()[id] || this.activeGroupIds().has(id);
  }

  toggleGroup(item: NavItem, event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    const id = this.groupId(item);
    this.expandedGroups.update((state) => ({ ...state, [id]: !this.isGroupExpanded(item) }));
  }

  trackMenuItem(item: NavItem): string {
    return item.path || this.groupId(item);
  }
}
