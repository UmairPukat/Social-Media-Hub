export const PROCESS_MODULES = {
  integrations: {
    id: 'integration',
    label: 'Integrations',
    icon: 'hub',
    apiBase: 'integrations',
    routeBase: '/app/integrations',
    callbackPath: '/api/integrations/callback',
    webhookPath: '/api/integrations/webhooks',
    whatsappCallbackPath: '/api/integrations/whatsapp/callback',
    whatsappWebhookPath: '/api/integrations/whatsapp/webhooks'
  },
  appConnections: {
    id: 'app_connection',
    label: 'App Connections',
    icon: 'apps',
    apiBase: 'app-connections',
    routeBase: '/app/app-connections',
    callbackPath: '/api/app-connections/callback',
    webhookPath: '/api/app-connections/webhooks',
    whatsappCallbackPath: '/api/app-connections/whatsapp/callback',
    whatsappWebhookPath: '/api/app-connections/whatsapp/webhooks'
  },
  developerApps: {
    id: 'developer_app',
    label: 'Developer Apps',
    icon: 'code',
    apiBase: 'developer-apps',
    routeBase: '/app/developer-apps',
    callbackPath: '/api/developer-apps/callback',
    webhookPath: '/api/developer-apps/webhooks',
    whatsappCallbackPath: '/api/developer-apps/whatsapp/callback',
    whatsappWebhookPath: '/api/developer-apps/whatsapp/webhooks'
  },
  socialMediaAccounts: {
    id: 'social_media_account',
    label: 'Social Media Accounts',
    icon: 'share',
    apiBase: 'social-media-accounts',
    routeBase: '/app/social-media-accounts',
    callbackPath: '/api/social-media-accounts/callback',
    webhookPath: '/api/social-media-accounts/webhooks',
    whatsappCallbackPath: '/api/social-media-accounts/whatsapp/callback',
    whatsappWebhookPath: '/api/social-media-accounts/whatsapp/webhooks'
  },
  accountConnections: {
    id: 'account_connection',
    label: 'Account Connections',
    icon: 'account_tree',
    apiBase: 'account-connections',
    routeBase: '/app/account-connections',
    callbackPath: '/api/account-connections/callback',
    webhookPath: '/api/account-connections/webhooks',
    whatsappCallbackPath: '/api/account-connections/whatsapp/callback',
    whatsappWebhookPath: '/api/account-connections/whatsapp/webhooks'
  }
} as const;

export type ProcessModuleKey = keyof typeof PROCESS_MODULES;
export type ProcessMenuType = (typeof PROCESS_MODULES)[ProcessModuleKey]['id'];

export const PROCESS_MODULE_LIST = [
  PROCESS_MODULES.integrations,
  PROCESS_MODULES.appConnections,
  PROCESS_MODULES.developerApps,
  PROCESS_MODULES.socialMediaAccounts,
  PROCESS_MODULES.accountConnections
];

export function processFromRoute(path: string): (typeof PROCESS_MODULES)[ProcessModuleKey] | null {
  if (path.includes('/app/integrations')) return PROCESS_MODULES.integrations;
  if (path.includes('/app/app-connections')) return PROCESS_MODULES.appConnections;
  if (path.includes('/app/developer-apps')) return PROCESS_MODULES.developerApps;
  if (path.includes('/app/social-media-accounts')) return PROCESS_MODULES.socialMediaAccounts;
  if (path.includes('/app/account-connections')) return PROCESS_MODULES.accountConnections;
  return null;
}
