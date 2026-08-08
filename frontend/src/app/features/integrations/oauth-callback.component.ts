import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { META_OAUTH_MESSAGE, MetaAuthUrlService, MetaPlatform } from '../../core/services/meta-auth-url.service';

/**
 * Shared Meta OAuth landing page for Facebook, Instagram, and WhatsApp.
 * Meta redirects here with ?code=...&state=facebook.<nonce> (platform lives in state).
 */
@Component({
  selector: 'app-oauth-callback',
  standalone: true,
  template: `
    <div class="wrap">
      <p>{{ status() }}</p>
    </div>
  `,
  styles: [`
    .wrap {
      min-height: 100vh;
      display: grid;
      place-items: center;
      font-family: "Segoe UI", system-ui, sans-serif;
      color: #1e293b;
      background: linear-gradient(160deg, #f8fafc, #e2e8f0);
      padding: 2rem;
      text-align: center;
    }
  `]
})
export class OAuthCallbackComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ApiService);
  private readonly metaAuth = inject(MetaAuthUrlService);

  readonly status = signal('Connecting your account…');

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    const parsed = this.metaAuth.parseState(params.get('state'));

    if (!parsed) {
      this.fail('unknown', 'Missing or invalid OAuth state.');
      return;
    }

    const platform = parsed.platform;
    if (!parsed.valid) {
      this.fail(platform, 'Invalid OAuth state.');
      return;
    }

    const error = params.get('error_description') || params.get('error');
    if (error) {
      this.fail(platform, error);
      return;
    }

    const code = params.get('code');
    if (!code) {
      this.fail(platform, 'Missing authorization code.');
      return;
    }

    this.metaAuth.clearState(platform);

    this.api.exchangeAuthCode(platform, {
      code,
      redirectUri: this.metaAuth.getRedirectUri()
    }).subscribe({
      next: (res) => {
        if (!res.success) {
          this.fail(platform, res.message || 'Connect failed');
          return;
        }
        this.status.set('Connected. You can close this window.');
        this.notify(platform, true);
        window.setTimeout(() => window.close(), 600);
      },
      error: (err: { error?: { message?: string } }) => {
        this.fail(platform, err?.error?.message || 'Connect failed');
      }
    });
  }

  private fail(platform: string, message: string): void {
    this.status.set(message);
    this.notify(platform, false, message);
  }

  private notify(platform: string, ok: boolean, message?: string): void {
    const payload = { type: META_OAUTH_MESSAGE, platform, ok, message };
    if (window.opener && !window.opener.closed) {
      window.opener.postMessage(payload, window.location.origin);
    }
  }
}
