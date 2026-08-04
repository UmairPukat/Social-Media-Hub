import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatIconModule,
    MatButtonModule
  ],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss'
})
export class ShellComponent {
  readonly menus = [
    { label: 'Dashboard', icon: 'dashboard', link: '/app/dashboard' },
    { label: 'Integrations', icon: 'hub', link: '/app/integrations' },
    { label: 'Create Post', icon: 'post_add', link: '/app/create-post' },
    { label: 'Posts', icon: 'article', link: '/app/posts' },
    { label: 'Inbox', icon: 'inbox', link: '/app/inbox' },
    { label: 'Connected Accounts', icon: 'account_circle', link: '/app/accounts' },
    { label: 'Analytics', icon: 'insights', link: '/app/analytics' },
    { label: 'Invite Signup', icon: 'person_add', link: '/app/signup' },
    { label: 'Settings', icon: 'settings', link: '/app/settings' }
  ];

  constructor(public auth: AuthService) {}
}
