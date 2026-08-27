export const PROCESS_MODULES = {
  integrations: {
    id: 'integration',
    label: 'Integrations',
    icon: 'hub',
    apiBase: 'integrations',
    routeBase: '/app/integrations',
    callbackPath: '/api/integrations/callback',
    webhookPath: '/api/integrations/webhooks'
  },
  appConnections: {
    id: 'app_connection',
    label: 'App Connections',
    icon: 'apps',
    apiBase: 'app-connections',
    routeBase: '/app/app-connections',
    callbackPath: '/api/app-connections/callback',
    webhookPath: '/api/app-connections/webhooks'
  },
  developerApps: {
    id: 'developer_app',
    label: 'Developer Apps',
    icon: 'code',
    apiBase: 'developer-apps',
    routeBase: '/app/developer-apps',
    callbackPath: '/api/developer-apps/callback',
    webhookPath: '/api/developer-apps/webhooks'
  }
} as const;

export type ProcessModuleKey = keyof typeof PROCESS_MODULES;
export type ProcessMenuType = (typeof PROCESS_MODULES)[ProcessModuleKey]['id'];

export const PROCESS_MODULE_LIST = [
  PROCESS_MODULES.integrations,
  PROCESS_MODULES.appConnections,
  PROCESS_MODULES.developerApps
];

export function processFromRoute(path: string): (typeof PROCESS_MODULES)[ProcessModuleKey] | null {
  if (path.includes('/app/integrations')) return PROCESS_MODULES.integrations;
  if (path.includes('/app/app-connections')) return PROCESS_MODULES.appConnections;
  if (path.includes('/app/developer-apps')) return PROCESS_MODULES.developerApps;
  return null;
}
