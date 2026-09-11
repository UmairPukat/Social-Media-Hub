import { MetaApiResponse } from '../../core/models/meta-ads.models';

export function normalizeAdAccountNumericId(adAccountId: string): string {
  const id = (adAccountId || '').trim();
  return id.startsWith('act_') ? id.slice(4) : id;
}

function metaAdsManagerUrl(path: string, adAccountId: string, params: Record<string, string> = {}): string {
  const search = new URLSearchParams({ act: normalizeAdAccountNumericId(adAccountId), ...params });
  return `https://adsmanager.facebook.com/adsmanager/manage/${path}?${search.toString()}`;
}

/** Opens the ad account in Meta Ads Manager. */
export function metaAdsManagerAdAccountUrl(adAccountId: string): string {
  return metaAdsManagerUrl('campaigns', adAccountId);
}

/** Opens the campaign in Meta Ads Manager. */
export function metaAdsManagerCampaignUrl(adAccountId: string, campaignId: string): string {
  return metaAdsManagerUrl('campaigns', adAccountId, { selected_campaign_ids: campaignId });
}

/** Opens the ad set in Meta Ads Manager. */
export function metaAdsManagerAdSetUrl(adAccountId: string, adSetId: string): string {
  return metaAdsManagerUrl('adsets', adAccountId, { selected_adset_ids: adSetId });
}

/** Opens the ad in Meta Ads Manager. */
export function metaAdsManagerAdUrl(adAccountId: string, adId: string): string {
  return metaAdsManagerUrl('ads', adAccountId, { selected_ad_ids: adId });
}

export function metaErrorMessage<T>(response: MetaApiResponse<T>): string {
  if (response.metaErrorMessage) {
    return `${response.message}${response.metaErrorCode ? ` (${response.metaErrorCode})` : ''}: ${response.metaErrorMessage}`;
  }
  return response.message || 'Request failed.';
}
