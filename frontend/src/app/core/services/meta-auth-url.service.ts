import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

export type MetaPlatform = 'facebook' | 'instagram' | 'whatsapp';

export const META_OAUTH_MESSAGE = 'smh-meta-oauth';

/**
 * Builds Meta / Facebook Login URLs and opens the OAuth popup.
 * Authorization code is sent to the backend — never exchange App Secret in the browser.
 */
@Injectable({ providedIn: 'root' })
export class MetaAuthUrlService {
  buildAuthUrl(platform: MetaPlatform, state: string): string {
    const cfg = environment.meta[platform];
    const version = cfg.graphVersion || 'v21.0';
    // Instagram uses Facebook Login (same dialog) — not Instagram Business Login.
    return `https://www.facebook.com/${version}/dialog/oauth`
      + `?client_id=${encodeURIComponent(cfg.appId)}`
      + `&redirect_uri=${encodeURIComponent(cfg.redirectUri)}`
      + `&state=${encodeURIComponent(state)}`
      + `&scope=${encodeURIComponent(cfg.scopes)}`
      + `&response_type=code`;
  }

  getRedirectUri(platform: MetaPlatform): string {
    return environment.meta[platform].redirectUri;
  }

  isConfigured(platform: MetaPlatform): boolean {
    const id = environment.meta[platform].appId;
    return !!id && !id.startsWith('YOUR_');
  }

  /**
   * Opens Meta Login in a popup. Resolves when the callback page posts a success/error message.
   */
  openPopup(platform: MetaPlatform): Promise<{ ok: boolean; message?: string }> {
    const state = crypto.randomUUID();
    sessionStorage.setItem(`smh_oauth_state_${platform}`, state);

    const url = this.buildAuthUrl(platform, state);
    const width = 600;
    const height = 720;
    const left = Math.max(0, (window.screen.width - width) / 2);
    const top = Math.max(0, (window.screen.height - height) / 2);
    const features = `width=${width},height=${height},left=${left},top=${top},scrollbars=yes,resizable=yes`;

    const popup = window.open(url, `meta_oauth_${platform}`, features);
    if (!popup) {
      return Promise.reject(new Error('Popup was blocked. Allow popups for this site and try again.'));
    }

    return new Promise((resolve, reject) => {
      let settled = false;

      const cleanup = () => {
        window.removeEventListener('message', onMessage);
        window.clearInterval(timer);
      };

      const settle = (fn: () => void) => {
        if (settled) return;
        settled = true;
        cleanup();
        fn();
      };

      const onMessage = (event: MessageEvent) => {
        if (event.origin !== window.location.origin) return;
        const data = event.data;
        if (!data || data.type !== META_OAUTH_MESSAGE || data.platform !== platform) return;
        if (data.ok) settle(() => resolve({ ok: true }));
        else settle(() => resolve({ ok: false, message: data.message || 'Connection failed' }));
      };

      const timer = window.setInterval(() => {
        if (popup.closed) {
          // Give the callback a moment to postMessage before treating close as cancel.
          window.setTimeout(() => {
            settle(() => reject(new Error('Login window was closed before completing.')));
          }, 400);
        }
      }, 500);

      window.addEventListener('message', onMessage);
    });
  }
}
