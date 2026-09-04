import { Injectable, inject, NgZone } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { MENU_TYPES, MenuType } from '../models/api.models';
import { PROCESS_MODULES } from '../config/process.config';
import { META_OAUTH_MESSAGE, OAUTH_RESULT_STORAGE_KEY } from '../config/oauth.constants';

export type MetaPlatform = 'facebook' | 'instagram' | 'instagram_login' | 'whatsapp' | 'youtube' | 'tiktok';

export { META_OAUTH_MESSAGE };

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

interface OAuthPopupRect {
  width: number;
  height: number;
  left: number;
  top: number;
}

/** TikTok's authorize page expects a wider viewport than Meta/Google popups. */
function oauthPopupRect(platform: MetaPlatform): OAuthPopupRect {
  const preferred =
    platform === 'tiktok'
      ? { width: 960, height: 780 }
      : platform === 'youtube'
        ? { width: 520, height: 640 }
        : { width: 600, height: 720 };

  const screenLeft = window.screenLeft ?? window.screenX ?? 0;
  const screenTop = window.screenTop ?? window.screenY ?? 0;
  const availWidth = window.screen.availWidth || window.outerWidth || preferred.width;
  const availHeight = window.screen.availHeight || window.outerHeight || preferred.height;

  const width = Math.min(preferred.width, Math.max(480, availWidth - 32));
  const height = Math.min(preferred.height, Math.max(520, availHeight - 48));

  const left = Math.round(screenLeft + Math.max(0, (window.outerWidth - width) / 2));
  const top = Math.round(screenTop + Math.max(0, (window.outerHeight - height) / 2));

  return { width, height, left, top };
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
  private readonly frontendOrigin = typeof window !== 'undefined' ? window.location.origin : '';

  isConfigured(platform: MetaPlatform): boolean {
    const id = environment.meta[platform].appId;
    return !!id && !id.startsWith('YOUR_');
  }

  /**
   * Opens OAuth in a popup. Resolves when the backend callback posts a result,
   * or rejects if the popup is closed early / the flow times out.
   */
  openPopup(
    platform: MetaPlatform,
    menuType: MenuType = MENU_TYPES.integration
  ): Promise<{ ok: boolean; message?: string }> {
    return this.zone.run(async () => {
      const apiSegment =
        menuType === MENU_TYPES.appConnection
          ? PROCESS_MODULES.appConnections.apiBase
          : menuType === MENU_TYPES.developerApp
            ? PROCESS_MODULES.developerApps.apiBase
            : PROCESS_MODULES.integrations.apiBase;

      let beginResponse: ApiResponse<BeginOAuthResponse>;
      try {
        beginResponse = await firstValueFrom(
          this.http.post<ApiResponse<BeginOAuthResponse>>(`${environment.apiUrl}/${apiSegment}/oauth/begin`, {
            platformCode: platform,
            menuType
          })
        );
      } catch (err: unknown) {
        const message =
          (err as { error?: { message?: string } })?.error?.message || 'Could not start OAuth login.';
        return { ok: false, message };
      }

      if (!beginResponse.success || !beginResponse.data?.authUrl) {
        return { ok: false, message: beginResponse.message || 'Could not start OAuth login.' };
      }

      const { width, height, left, top } = oauthPopupRect(platform);
      const features = `width=${width},height=${height},left=${left},top=${top},scrollbars=yes,resizable=yes`;

      const popup = window.open(beginResponse.data.authUrl, `meta_oauth_${platform}`, features);
      if (!popup) {
        throw new Error('Popup was blocked. Allow popups for this site and try again.');
      }

      return new Promise((resolve, reject) => {
        let settled = false;

        const cleanup = () => {
          window.removeEventListener('message', onMessage);
          window.removeEventListener('focus', onWindowFocus);
          window.removeEventListener('storage', onStorage);
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

        const readStoredResult = () => {
          try {
            const raw = localStorage.getItem(OAUTH_RESULT_STORAGE_KEY);
            if (!raw) return;
            localStorage.removeItem(OAUTH_RESULT_STORAGE_KEY);
            handlePayload(JSON.parse(raw));
          } catch {
            // ignore
          }
        };

        const onMessage = (event: MessageEvent) => {
          const allowed =
            event.origin === this.backendOrigin ||
            event.origin === this.frontendOrigin ||
            event.origin === window.location.origin;
          if (!allowed) return;
          handlePayload(event.data);
        };

        const onStorage = (event: StorageEvent) => {
          if (event.key !== OAUTH_RESULT_STORAGE_KEY || !event.newValue) return;
          try {
            handlePayload(JSON.parse(event.newValue));
          } catch {
            // ignore
          }
          try {
            localStorage.removeItem(OAUTH_RESULT_STORAGE_KEY);
          } catch {
            // ignore
          }
        };

        const isPopupClosed = (): boolean => {
          try {
            return popup.closed;
          } catch {
            return false;
          }
        };

        const onWindowFocus = () => {
          if (settled) return;
          readStoredResult();
          if (settled) return;

          window.setTimeout(() => {
            if (settled) return;
            readStoredResult();
            if (settled) return;
            if (isPopupClosed()) {
              settle(() => reject(new Error('Login window was closed before completing.')));
            }
          }, 500);
        };

        const timeout = window.setTimeout(() => {
          settle(() =>
            reject(new Error('Login timed out. Close the popup and try connecting again.')));
        }, 10 * 60 * 1000);

        window.addEventListener('message', onMessage);
        window.addEventListener('focus', onWindowFocus);
        window.addEventListener('storage', onStorage);
      });
    });
  }
}
