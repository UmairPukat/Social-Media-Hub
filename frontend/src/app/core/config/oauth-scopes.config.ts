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
