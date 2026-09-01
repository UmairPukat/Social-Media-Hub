import { ConnectionDetails } from '../models/api.models';

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
