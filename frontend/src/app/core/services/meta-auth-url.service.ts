import { Injectable, inject, NgZone } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { MENU_TYPES, MenuType } from '../models/api.models';
import { PROCESS_MODULES } from '../config/process.config';

export type MetaPlatform = 'facebook' | 'instagram' | 'instagram_login' | 'whatsapp' | 'youtube';

export const META_OAUTH_MESSAGE = 'smh-meta-oauth';

interface BeginOAuthResponse {
  authUrl: string;
  redirectUri: string;
  platformCode: string;
}

interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data?: T;
}

interface OAuthPopupPayload {
  type?: string;
  platform?: string;
  ok?: boolean;
  message?: string;
}

/**
 * OAuth popup helper. The backend owns the redirect and callback HTML, which
 * posts the result back to this window via postMessage.
 */
@Injectable({ providedIn: 'root' })
export class MetaAuthUrlService {
  private readonly http = inject(HttpClient);
  private readonly zone = inject(NgZone);

  /** Origin of the backend API — the OAuth callback page is served from here. */
  private readonly backendOrigin = new URL(environment.apiUrl).origin;

  isConfigured(platform: MetaPlatform): boolean {
    const id = environment.meta[platform].appId;
    return !!id && !id.startsWith('YOUR_');
  }

  /**
   * Opens OAuth in a popup. Resolves when the backend callback posts a result,
   * or rejects if the popup is closed early / the flow times out.
   */
  openPopup(platform: MetaPlatform, menuType: MenuType = MENU_TYPES.integration): Promise<{ ok: boolean; message?: string }> {
    const width = 600;
    const height = 720;
    const left = Math.max(0, (window.screen.width - width) / 2);
    const top = Math.max(0, (window.screen.height - height) / 2);
    const features = `width=${width},height=${height},left=${left},top=${top},scrollbars=yes,resizable=yes`;

    const popup = window.open('about:blank', `meta_oauth_${platform}`, features);
    if (!popup) {
      return Promise.reject(new Error('Popup was blocked. Allow popups for this site and try again.'));
    }

    return new Promise((resolve, reject) => {
      let settled = false;
      let oauthStarted = false;

      const cleanup = () => {
        window.removeEventListener('message', onMessage);
        window.removeEventListener('focus', onWindowFocus);
        window.clearTimeout(timeout);
      };

      const settle = (fn: () => void) => {
        if (settled) return;
        settled = true;
        cleanup();
        this.zone.run(fn);
      };

      const handlePayload = (data: unknown) => {
        if (!data || typeof data !== 'object') return;
        const payload = data as OAuthPopupPayload;
        if (payload.type !== META_OAUTH_MESSAGE) return;
        if (payload.platform && payload.platform !== platform) return;
        if (payload.ok) settle(() => resolve({ ok: true }));
        else settle(() => resolve({ ok: false, message: payload.message || 'Connection failed' }));
      };

      const onMessage = (event: MessageEvent) => {
        if (event.origin !== this.backendOrigin) return;
        handlePayload(event.data);
      };

      const isPopupClosed = (): boolean => {
        try {
          return popup.closed;
        } catch {
          return false;
        }
      };

      const onWindowFocus = () => {
        if (!oauthStarted || settled) return;
        window.setTimeout(() => {
          if (settled) return;
          if (isPopupClosed()) {
            settle(() => reject(new Error('Login window was closed before completing.')));
          }
        }, 600);
      };

      const timeout = window.setTimeout(() => {
        settle(() =>
          reject(new Error('Login timed out. Close the popup and try connecting again.')));
      }, 10 * 60 * 1000);

      window.addEventListener('message', onMessage);
      window.addEventListener('focus', onWindowFocus);

      const apiSegment = menuType === MENU_TYPES.appConnection
        ? PROCESS_MODULES.appConnections.apiBase
        : menuType === MENU_TYPES.developerApp
          ? PROCESS_MODULES.developerApps.apiBase
          : PROCESS_MODULES.integrations.apiBase;

      this.http
        .post<ApiResponse<BeginOAuthResponse>>(`${environment.apiUrl}/${apiSegment}/oauth/begin`, {
          platformCode: platform,
          menuType
        })
        .subscribe({
          next: (res) => {
            if (!res.success || !res.data?.authUrl) {
              try {
                popup.close();
              } catch {
                // ignore
              }
              settle(() => resolve({ ok: false, message: res.message || 'Could not start OAuth login.' }));
              return;
            }
            oauthStarted = true;
            popup.location.href = res.data.authUrl;
          },
          error: (err: { error?: { message?: string } }) => {
            try {
              popup.close();
            } catch {
              // ignore
            }
            settle(() =>
              resolve({ ok: false, message: err?.error?.message || 'Could not start OAuth login.' }));
          }
        });
    });
  }
}
