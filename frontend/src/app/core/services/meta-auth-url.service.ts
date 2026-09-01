import { Injectable, inject } from '@angular/core';
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

/**
 * Meta Login popup. The backend builds the auth URL and owns the OAuth redirect:
 * Meta redirects to GET /api/Integrations/Callback, which exchanges the code and
 * posts a success/error message back to this window.
 */
@Injectable({ providedIn: 'root' })
export class MetaAuthUrlService {
  private readonly http = inject(HttpClient);

  /** Origin of the backend API — the OAuth result message comes from this origin. */
  private readonly backendOrigin = new URL(environment.apiUrl).origin;

  isConfigured(platform: MetaPlatform): boolean {
    const id = environment.meta[platform].appId;
    return !!id && !id.startsWith('YOUR_');
  }

  /**
   * Opens Meta Login in a popup. Resolves when the backend Callback page posts
   * a success/error message, or rejects if the window closes early.
   */
  openPopup(platform: MetaPlatform, menuType: MenuType = MENU_TYPES.integration): Promise<{ ok: boolean; message?: string }> {
    const width = 600;
    const height = 720;
    const left = Math.max(0, (window.screen.width - width) / 2);
    const top = Math.max(0, (window.screen.height - height) / 2);
    const features = `width=${width},height=${height},left=${left},top=${top},scrollbars=yes,resizable=yes`;

    // Open synchronously on the click so popup blockers allow it, then navigate.
    const popup = window.open('about:blank', `meta_oauth_${platform}`, features);
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
        if (event.origin !== this.backendOrigin) return;
        const data = event.data;
        if (!data || data.type !== META_OAUTH_MESSAGE) return;
        // Error results may not carry a platform (e.g. invalid state) — accept those too.
        if (data.platform && data.platform !== platform) return;
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
              popup.close();
              settle(() => resolve({ ok: false, message: res.message || 'Could not start Meta login.' }));
              return;
            }
            popup.location.href = res.data.authUrl;
          },
          error: (err: { error?: { message?: string } }) => {
            popup.close();
            settle(() =>
              resolve({ ok: false, message: err?.error?.message || 'Could not start Meta login.' }));
          }
        });
    });
  }
}
