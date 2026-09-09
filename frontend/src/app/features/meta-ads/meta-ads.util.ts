import { MetaApiResponse } from '../../core/models/meta-ads.models';

export function metaErrorMessage<T>(response: MetaApiResponse<T>): string {
  if (response.metaErrorMessage) {
    return `${response.message}${response.metaErrorCode ? ` (${response.metaErrorCode})` : ''}: ${response.metaErrorMessage}`;
  }
  return response.message || 'Request failed.';
}
