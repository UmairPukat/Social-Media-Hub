import { MetaApiResponse } from '../../core/models/meta-ads.models';

export function normalizeAdAccountNumericId(adAccountId: string): string {
  const id = (adAccountId || '').trim();
  return id.startsWith('act_') ? id.slice(4) : id;
}

/** Opens the campaign in Meta Ads Manager. */
export function metaAdsManagerCampaignUrl(adAccountId: string, campaignId: string): string {
  const act = normalizeAdAccountNumericId(adAccountId);
  const params = new URLSearchParams({
    act,
    selected_campaign_ids: campaignId
  });
  return `https://adsmanager.facebook.com/adsmanager/manage/campaigns?${params.toString()}`;
}

export function metaErrorMessage<T>(response: MetaApiResponse<T>): string {
  if (response.metaErrorMessage) {
    return `${response.message}${response.metaErrorCode ? ` (${response.metaErrorCode})` : ''}: ${response.metaErrorMessage}`;
  }
  return response.message || 'Request failed.';
}
