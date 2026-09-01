import { META_OAUTH_MESSAGE, OAUTH_RESULT_STORAGE_KEY } from '../config/oauth.constants';

export interface OAuthPopupPayload {
  type?: string;
  platform?: string;
  ok?: boolean;
  message?: string;
}

/** Reads OAuth popup result passed in the URL hash (#payload=...). */
export function readOAuthPayloadFromHash(): OAuthPopupPayload | null {
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

/** Notifies the opener tab / other tabs that OAuth finished. */
export function deliverOAuthPayload(payload: OAuthPopupPayload): void {
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

/** Handles OAuth relay hash if present. Returns true when handled. */
export function handleOAuthRelayHash(): OAuthPopupPayload | null {
  const payload = readOAuthPayloadFromHash();
  if (!payload) return null;
  deliverOAuthPayload(payload);
  return payload;
}
