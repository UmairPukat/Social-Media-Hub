import { environment } from '../../../environments/environment';
import { PROCESS_MODULE_LIST, ProcessMenuType } from './process.config';

/** OAuth callback URL registered in Google / Meta for the active process module. */
export function defaultOAuthRedirectUri(menuType: ProcessMenuType): string {
  const module = PROCESS_MODULE_LIST.find((item) => item.id === menuType);
  if (!module) return '';

  const apiUrl = environment.apiUrl.replace(/\/+$/, '');
  const backendOrigin = apiUrl.replace(/\/api\/?$/i, '');
  return `${backendOrigin}${module.callbackPath}`;
}

/** Meta webhook URL for the active process module. */
export function defaultWebhookRedirectUri(menuType: ProcessMenuType): string {
  const module = PROCESS_MODULE_LIST.find((item) => item.id === menuType);
  if (!module) return '';

  const apiUrl = environment.apiUrl.replace(/\/+$/, '');
  const backendOrigin = apiUrl.replace(/\/api\/?$/i, '');
  return `${backendOrigin}${module.webhookPath}`;
}

/** WhatsApp OAuth callback URL for the active process module. */
export function defaultWhatsAppOAuthRedirectUri(menuType: ProcessMenuType): string {
  const module = PROCESS_MODULE_LIST.find((item) => item.id === menuType);
  if (!module) return '';

  const apiUrl = environment.apiUrl.replace(/\/+$/, '');
  const backendOrigin = apiUrl.replace(/\/api\/?$/i, '');
  return `${backendOrigin}${module.whatsappCallbackPath}`;
}

/** WhatsApp webhook URL for the active process module. */
export function defaultWhatsAppWebhookUri(menuType: ProcessMenuType): string {
  const module = PROCESS_MODULE_LIST.find((item) => item.id === menuType);
  if (!module) return '';

  const apiUrl = environment.apiUrl.replace(/\/+$/, '');
  const backendOrigin = apiUrl.replace(/\/api\/?$/i, '');
  return `${backendOrigin}${module.whatsappWebhookPath}`;
}
