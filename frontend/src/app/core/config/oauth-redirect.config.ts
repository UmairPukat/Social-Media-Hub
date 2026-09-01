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
