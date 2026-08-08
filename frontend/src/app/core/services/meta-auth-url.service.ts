import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

export type MetaPlatform = 'facebook' | 'instagram' | 'whatsapp';

export const META_OAUTH_MESSAGE = 'smh-meta-oauth';

/** Shared OAuth redirect for Facebook, Instagram, and WhatsApp. */
export function sharedMetaRedirectUri(): string {
  return environment.meta.redirectUri
    || environment.meta.facebook.redirectUri
    || `${window.location.origin}/integrations/callback`;
}

/**
 * Builds Meta / Facebook Login URLs and opens the OAuth popup.
 * Authorization code is sent to the backend — never exchange App Secret in the browser.
 * Platform is encoded in OAuth state so one callback URL serves every product.
 */
@Injectable({ providedIn: 'root' })
export class MetaAuthUrlService {
  buildAuthUrl(platform: MetaPlatform, state: string): string {
    const cfg = environment.meta[platform];
    const version = cfg.graphVersion || 'v21.0';
    const redirectUri = this.getRedirectUri();
    // Instagram uses Facebook Login (same dialog) — not Instagram Business Login.
    return `https://www.facebook.com/${version}/dialog/oauth`
      + `?client_id=${encodeURIComponent(cfg.appId)}`
      + `&redirect_uri=${encodeURIComponent(redirectUri)}`
      + `&state=${encodeURIComponent(state)}`
      + `&scope=${encodeURIComponent(cfg.scopes)}`
      + `&response_type=code`;
  }

  getRedirectUri(): string {
    return sharedMetaRedirectUri();
  }

  /** Encodes platform into state so the shared callback can finish the right flow. */
  createState(platform: MetaPlatform): string {
    const state = `${platform}.${crypto.randomUUID()}`;
    sessionStorage.setItem('smh_oauth_state', state);
    return state;
  }

  /** Reads platform from Meta's returned state (`facebook.<nonce>`). */
  parseState(state: string | null): { platform: MetaPlatform; valid: boolean } | null {
    if (!state) return null;
    const dot = state.indexOf('.');
    if (dot <= 0) return null;
    const platform = state.slice(0, dot).toLowerCase() as MetaPlatform;
    if (!['facebook', 'instagram', 'whatsapp'].includes(platform)) return null;

    const expected = sessionStorage.getItem('smh_oauth_state');
    return { platform, valid: !!expected && expected === state };
  }

  clearState(_platform?: MetaPlatform): void {
    sessionStorage.removeItem('smh_oauth_state');
  }

  isConfigured(platform: MetaPlatform): boolean {
    const id = environment.meta[platform].appId;
    return !!id && !id.startsWith('YOUR_');
  }

  /**
   * Opens Meta Login in a popup. Resolves when the callback page posts a success/error message.
   */
  openPopup(platform: MetaPlatform): Promise<{ ok: boolean; message?: string }> {
    const state = this.createState(platform);

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
