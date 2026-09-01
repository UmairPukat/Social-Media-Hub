import { Component, OnInit, signal } from '@angular/core';
import { handleOAuthRelayHash } from '../../core/services/oauth-relay.service';

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
    const payload = handleOAuthRelayHash();
    if (!payload) {
      this.status.set('Missing OAuth result. You can close this window.');
      return;
    }

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
}
