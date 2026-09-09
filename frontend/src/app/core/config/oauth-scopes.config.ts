/** Google YouTube OAuth scopes — stored as an array; joined with spaces for authorize URLs. */
export const YOUTUBE_OAUTH_SCOPES = [
  'https://www.googleapis.com/auth/youtube.readonly',
  'https://www.googleapis.com/auth/youtube.force-ssl',
  'https://www.googleapis.com/auth/yt-analytics.readonly'
] as const;

export type YouTubeOAuthScope = (typeof YOUTUBE_OAUTH_SCOPES)[number];

/**
 * Google OAuth expects multiple scopes separated by spaces, never commas.
 * Accepts comma- or whitespace-separated input and returns a space-delimited string.
 */
export function formatYouTubeOAuthScopes(scopes?: string | null): string {
  if (!scopes?.trim()) {
    return YOUTUBE_OAUTH_SCOPES.join(' ');
  }

  return scopes
    .split(/[,\s]+/)
    .map((scope) => scope.trim())
    .filter(Boolean)
    .join(' ');
}

export function youtubeDefaultScopeString(): string {
  return YOUTUBE_OAUTH_SCOPES.join(' ');
}

/** TikTok Login Kit / Content Posting scopes — comma-separated in authorize URLs. */
export const TIKTOK_OAUTH_SCOPES = [
  'user.info.basic',
  'video.list',
  'video.upload',
  'video.publish'
] as const;

export function formatTikTokOAuthScopes(scopes?: string | null): string {
  if (!scopes?.trim()) {
    return TIKTOK_OAUTH_SCOPES.join(',');
  }

  return scopes
    .split(/[,\s]+/)
    .map((scope) => scope.trim())
    .filter(Boolean)
    .join(',');
}

export function tiktokDefaultScopeString(): string {
  return TIKTOK_OAUTH_SCOPES.join(',');
}

/** Default Facebook / Meta Login scopes for Integrations (includes Ads & Catalog permissions). */
export const FACEBOOK_OAUTH_SCOPES = [
  'public_profile',
  'pages_show_list',
  'pages_read_engagement',
  'pages_read_user_content',
  'pages_manage_engagement',
  'pages_manage_metadata',
  'pages_messaging',
  'business_management',
  'ads_management',
  'ads_read',
  'read_insights',
  'catalog_management'
] as const;

export function facebookDefaultScopeString(): string {
  return FACEBOOK_OAUTH_SCOPES.join(',');
}

export function formatPlatformOAuthScopes(platformCode: string, scopes?: string | null): string {
  const code = (platformCode || '').toLowerCase();
  if (code === 'youtube') return formatYouTubeOAuthScopes(scopes);
  if (code === 'tiktok') return formatTikTokOAuthScopes(scopes);
  if (code === 'facebook' || code === 'instagram' || code === 'whatsapp') {
    return scopes?.trim() || facebookDefaultScopeString();
  }
  return scopes?.trim() || '';
}
