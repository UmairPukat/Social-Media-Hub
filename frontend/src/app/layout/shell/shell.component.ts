import { Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../core/services/auth.service';
import { ThemeService } from '../../core/services/theme.service';

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
  readonly opened = signal(true);

  readonly menu = [
    { path: '/app/dashboard', icon: 'dashboard', label: 'Dashboard' },
    { path: '/app/integrations', icon: 'hub', label: 'Integrations' },
    { path: '/app/app-connections', icon: 'apps', label: 'App Connections' },
    { path: '/app/create-post', icon: 'post_add', label: 'Create Post' },
    { path: '/app/posts', icon: 'dynamic_feed', label: 'Posts' },
    { path: '/app/inbox', icon: 'inbox', label: 'Inbox' },
    { path: '/app/analytics', icon: 'insights', label: 'Analytics' },
    { path: '/app/accounts', icon: 'link', label: 'Connected Accounts' },
    { path: '/app/signup', icon: 'person_add', label: 'Invite Signup' },
    { path: '/app/settings', icon: 'settings', label: 'Settings' }
  ];

  toggle(): void {
    this.opened.update(v => !v);
  }

  logout(): void {
    this.auth.logout();
  }
}
