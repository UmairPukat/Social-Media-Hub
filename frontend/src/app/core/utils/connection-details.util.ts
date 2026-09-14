import { ConnectionDetails, SocialAccount, SocialProfile } from '../models/api.models';

export function formatInstagramUsername(username: string | null | undefined): string {
  if (!username?.trim()) return '—';
  const trimmed = username.trim();
  return trimmed.startsWith('@') ? trimmed : `@${trimmed}`;
}

export function instagramLoginProfile(info: ConnectionDetails) {
  const profiles = info.profiles ?? [];
  if (info.instagramId) {
    const matched = profiles.find(p => p.externalProfileId === info.instagramId);
    if (matched) return matched;
  }

  return (
    profiles.find(p => p.profileType?.toLowerCase() === 'instagramlogin') ??
    profiles[0]
  );
}

/** Username shown during Instagram Login OAuth (e.g. @uk377066060). */
export function instagramAccountName(info: ConnectionDetails): string {
  if (info.instagramUsername) return formatInstagramUsername(info.instagramUsername);

  const profile = instagramLoginProfile(info);
  if (profile?.username) return formatInstagramUsername(profile.username);
  if (profile?.name) return profile.name;
  if (info.pageName) return info.pageName;
  return '—';
}

/** Profile display name from Instagram Login (e.g. Umair Khan). */
export function instagramDisplayName(info: ConnectionDetails): string {
  const profile = instagramLoginProfile(info);
  return profile?.name || info.accountName || '—';
}

export const META_PAGE_PLATFORM_CODES = ['facebook', 'instagram', 'instagram_login'] as const;

/** Build a lookup of connected page names from connection-details responses. */
export function pageNameMapFromConnectionDetails(
  entries: Array<{ platformCode: string; details?: ConnectionDetails | null }>
): Map<string, string> {
  const map = new Map<string, string>();

  for (const entry of entries) {
    const code = entry.platformCode.toLowerCase();
    const details = entry.details;
    if (!details) continue;

    const pageName =
      details.pageName?.trim() ||
      (code === 'instagram_login' ? instagramDisplayName(details) : undefined);

    if (!pageName) continue;

    map.set(code, pageName);
    if (code === 'instagram' || code === 'instagram_login') {
      map.set('instagram', pageName);
    }
  }

  return map;
}

/** Connected page/profile label for account lists and composers. */
export function resolveSocialAccountLabel(account: SocialAccount, profile?: SocialProfile): string {
  const profileName = profile?.name?.trim();
  if (profileName) return profileName;

  const accountName = account.displayName?.trim();
  if (accountName) return accountName;

  return '—';
}

/** Prefer the selected page name over stale Meta login labels. */
export function resolveConnectedPageLabel(
  account: SocialAccount,
  profile: SocialProfile | undefined,
  pageNameByPlatform: ReadonlyMap<string, string>
): string {
  const code = account.platformCode.toLowerCase();
  const pageName = pageNameByPlatform.get(code)?.trim();
  if (pageName) return pageName;

  const profileName = profile?.name?.trim();
  const loginName = account.displayName?.trim();
  const isMetaPagePlatform = META_PAGE_PLATFORM_CODES.includes(
    code as (typeof META_PAGE_PLATFORM_CODES)[number]
  );

  if (isMetaPagePlatform && profileName && loginName && profileName.localeCompare(loginName, undefined, { sensitivity: 'accent' }) === 0) {
    return 'Connected page';
  }

  return resolveSocialAccountLabel(account, profile);
}
