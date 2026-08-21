import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export const APP_CONNECTION_OAUTH_MESSAGE = 'smh-app-connection-oauth';

interface BeginOAuthResponse {
  authUrl: string;
  redirectUri: string;
  platformCode: string;
  appConnectionId: string;
}

interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data?: T;
}

@Injectable({ providedIn: 'root' })
export class AppConnectionAuthService {
  private readonly http = inject(HttpClient);
  private readonly backendOrigin = new URL(environment.apiUrl).origin;

  openPopup(appConnectionId: string): Promise<{ ok: boolean; message?: string }> {
    const width = 600;
    const height = 720;
    const left = Math.max(0, (window.screen.width - width) / 2);
    const top = Math.max(0, (window.screen.height - height) / 2);
    const features = `width=${width},height=${height},left=${left},top=${top},scrollbars=yes,resizable=yes`;

    const popup = window.open('about:blank', `app_connection_oauth_${appConnectionId}`, features);
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
        if (!data || data.type !== APP_CONNECTION_OAUTH_MESSAGE) return;
        if (data.appConnectionId && data.appConnectionId !== appConnectionId) return;
        if (data.ok) settle(() => resolve({ ok: true }));
        else settle(() => resolve({ ok: false, message: data.message || 'Connection failed' }));
      };

      const timer = window.setInterval(() => {
        if (popup.closed) {
          window.setTimeout(() => {
            settle(() => reject(new Error('Login window was closed before completing.')));
          }, 400);
        }
      }, 500);

      window.addEventListener('message', onMessage);

      this.http
        .post<ApiResponse<BeginOAuthResponse>>(`${environment.apiUrl}/AppConnections/BeginOAuth`, {
          appConnectionId
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
