import { Component, OnInit, signal } from '@angular/core';
import { META_OAUTH_MESSAGE, OAUTH_RESULT_STORAGE_KEY } from '../../core/config/oauth.constants';

interface OAuthPopupPayload {
  type?: string;
  platform?: string;
  ok?: boolean;
  message?: string;
}

/**
 * OAuth relay page on the frontend origin. Google/Meta redirects can sever
 * window.opener; this page receives the result and notifies the parent tab.
 */
@Component({
  selector: 'app-oauth-complete',
  standalone: true,
  template: `
    <main class="oauth-complete">
      <p>{{ status() }}</p>
    </main>
  `,
  styles: [
    `
      .oauth-complete {
        display: grid;
        place-items: center;
        min-height: 100vh;
        margin: 0;
        font-family: 'Segoe UI', system-ui, sans-serif;
        color: #1e293b;
        background: #f8fafc;
        text-align: center;
        padding: 24px;
      }
    `
  ]
})
export class OAuthCompleteComponent implements OnInit {
  readonly status = signal('Finishing connection…');

  ngOnInit(): void {
    const payload = this.readPayload();
    if (!payload) {
      this.status.set('Missing OAuth result. You can close this window.');
      return;
    }

    this.deliver(payload);
    this.status.set(
      payload.ok
        ? 'Connected. You can close this window.'
        : payload.message || 'Connection failed. You can close this window.'
    );

    window.setTimeout(() => {
      try {
        window.close();
      } catch {
        // ignore
      }
    }, 1200);
  }

  private readPayload(): OAuthPopupPayload | null {
    const hash = window.location.hash.startsWith('#')
      ? window.location.hash.slice(1)
      : window.location.hash;
    if (!hash) return null;

    const params = new URLSearchParams(hash);
    const encoded = params.get('payload');
    if (!encoded) return null;

    try {
      const parsed = JSON.parse(decodeURIComponent(encoded)) as OAuthPopupPayload;
      return parsed?.type === META_OAUTH_MESSAGE ? parsed : null;
    } catch {
      return null;
    }
  }

  private deliver(payload: OAuthPopupPayload): void {
    try {
      localStorage.setItem(OAUTH_RESULT_STORAGE_KEY, JSON.stringify(payload));
    } catch {
      // ignore
    }

    if (!window.opener) return;

    try {
      window.opener.postMessage(payload, '*');
    } catch {
      // ignore
    }

    try {
      window.opener.postMessage(payload, window.location.origin);
    } catch {
      // ignore
    }
  }
}
