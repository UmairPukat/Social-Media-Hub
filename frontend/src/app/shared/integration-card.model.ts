/** Shared card model for Integrations and App Connections platform cards. */
export interface IntegrationCardView {
  trackId: string;
  code: string;
  displayName: string;
  description: string;
  isConnected: boolean;
  canConnect: boolean;
  accountName?: string;
  /** Value compared to connecting() while OAuth is in progress. */
  connectingKey: string;
  /** True when Meta login succeeded but no page has been picked yet. */
  requiresPageSelection?: boolean;
}

export interface IntegrationPlatformGroup {
  id: string;
  label: string;
  accent: string;
  icon: string;
  items: IntegrationCardView[];
}

export const APP_CONNECTION_PLATFORM_META: Record<string, { label: string; accent: string; icon: string }> = {
  facebook: { label: 'Facebook', accent: '#2563eb', icon: 'public' },
  instagram: { label: 'Instagram', accent: '#e11d48', icon: 'photo_camera' },
  instagram_login: { label: 'Instagram Login', accent: '#e11d48', icon: 'camera_alt' },
  whatsapp: { label: 'WhatsApp', accent: '#16a34a', icon: 'chat' }
};

export const APP_CONNECTION_PLATFORM_ORDER = ['facebook', 'instagram', 'instagram_login', 'whatsapp'];
