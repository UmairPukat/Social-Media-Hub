import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService } from '../../core/services/api.service';

@Component({
  selector: 'app-oauth-callback',
  standalone: true,
  imports: [CommonModule, MatProgressSpinnerModule],
  template: `
    <div class="wrap">
      @if (error()) {
        <p class="err">{{ error() }}</p>
      } @else {
        <mat-spinner diameter="40"></mat-spinner>
        <p>Connecting {{ platform }} account...</p>
      }
    </div>
  `,
  styles: `
    .wrap {
      min-height: 100vh;
      display: grid;
      place-items: center;
      align-content: center;
      gap: 16px;
      background: var(--bg);
    }
    .err { color: var(--danger); }
  `
})
export class OauthCallbackComponent implements OnInit {
  platform = '';
  readonly error = signal('');

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private api: ApiService
  ) {}

  ngOnInit(): void {
    this.platform = this.route.snapshot.paramMap.get('platform') || '';
    const code = this.route.snapshot.queryParamMap.get('code');
    const state = this.route.snapshot.queryParamMap.get('state');

    if (!code || !state || !this.platform) {
      this.error.set('Missing OAuth parameters.');
      return;
    }

    this.api.completeOAuth(this.platform, code, state).subscribe({
      next: () => this.router.navigate(['/app/integrations']),
      error: (err) => this.error.set(err?.error?.message || 'OAuth connection failed.')
    });
  }
}
