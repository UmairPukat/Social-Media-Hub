import { Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
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
  readonly opened = signal(true);

  readonly processModules = PROCESS_MODULE_LIST;

  readonly globalMenu = [
    { path: '/app/dashboard', icon: 'dashboard', label: 'Dashboard' },
    { path: '/app/signup', icon: 'person_add', label: 'Invite Signup' },
    { path: '/app/settings', icon: 'settings', label: 'Settings' }
  ];

  readonly processSubItems = [
    { suffix: 'connect', icon: 'hub', label: 'Connect' },
    { suffix: 'create-post', icon: 'post_add', label: 'Create Post' },
    { suffix: 'posts', icon: 'dynamic_feed', label: 'Posts' },
    { suffix: 'inbox', icon: 'inbox', label: 'Inbox' },
    { suffix: 'analytics', icon: 'insights', label: 'Analytics' },
    { suffix: 'accounts', icon: 'link', label: 'Connected Accounts' }
  ];

  expanded = signal<Record<string, boolean>>({
    integration: true,
    app_connection: false,
    developer_app: false
  });

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
