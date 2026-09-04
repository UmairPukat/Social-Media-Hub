import { PLATFORM_COLORS } from '../models/api.models';

export type CreatePlatform =
  | 'facebook'
  | 'instagram'
  | 'whatsapp'
  | 'tiktok'
  | 'youtube'
  | 'linkedin'
  | 'twitter';

export interface ComposerProfile {
  id: string;
  platformCode: CreatePlatform;
  name: string;
  username?: string;
  profileType: string;
  /** Fake profile used only for UI / demo when not connected. */
  isDemo: boolean;
}

export interface CreatePlatformMeta {
  code: CreatePlatform;
  label: string;
  icon: string;
  color: string;
  /** Connected accounts can publish through the API. */
  supportsPublish: boolean;
  supportsFiles: boolean;
  requiresMedia: boolean;
  /** HTML accept attribute for file picker */
  accept: string;
  hint: string;
}

export const CREATE_PLATFORMS: CreatePlatformMeta[] = [
  {
    code: 'facebook',
    label: 'Facebook',
    icon: 'facebook',
    color: PLATFORM_COLORS['facebook'],
    supportsPublish: true,
    supportsFiles: true,
    requiresMedia: false,
    accept: 'image/*,video/*',
    hint: 'Page post · text, photo or video'
  },
  {
    code: 'instagram',
    label: 'Instagram',
    icon: 'photo_camera',
    color: PLATFORM_COLORS['instagram'],
    supportsPublish: true,
    supportsFiles: true,
    requiresMedia: true,
    accept: 'image/*,video/*',
    hint: 'Feed post · image or video required'
  },
  {
    code: 'whatsapp',
    label: 'WhatsApp Business',
    icon: 'chat',
    color: PLATFORM_COLORS['whatsapp'],
    supportsPublish: false,
    supportsFiles: true,
    requiresMedia: false,
    accept: 'image/*,video/*,.pdf,.doc,.docx',
    hint: 'Broadcast / status · text + optional file'
  },
  {
    code: 'tiktok',
    label: 'TikTok',
    icon: 'music_note',
    color: PLATFORM_COLORS['tiktok'],
    supportsPublish: true,
    supportsFiles: true,
    requiresMedia: true,
    accept: 'video/*',
    hint: 'Video clip + caption (MP4, MOV, or WebM)'
  },
  {
    code: 'youtube',
    label: 'YouTube',
    icon: 'smart_display',
    color: PLATFORM_COLORS['youtube'],
    supportsPublish: true,
    supportsFiles: true,
    requiresMedia: true,
    accept: 'video/*,image/*',
    hint: 'Video title, description & file'
  },
  {
    code: 'linkedin',
    label: 'LinkedIn',
    icon: 'work',
    color: PLATFORM_COLORS['linkedin'],
    supportsPublish: false,
    supportsFiles: true,
    requiresMedia: false,
    accept: 'image/*,video/*,.pdf',
    hint: 'Professional update · text + optional media'
  },
  {
    code: 'twitter',
    label: 'X (Twitter)',
    icon: 'tag',
    color: PLATFORM_COLORS['twitter'],
    supportsPublish: false,
    supportsFiles: true,
    requiresMedia: false,
    accept: 'image/*,video/*',
    hint: 'Short post · text + optional media'
  }
];

export const DEMO_COMPOSER_PROFILES: ComposerProfile[] = [
  {
    id: 'demo-fb',
    platformCode: 'facebook',
    name: 'SocialHub Page',
    username: 'socialhub',
    profileType: 'FacebookPage',
    isDemo: true
  },
  {
    id: 'demo-ig',
    platformCode: 'instagram',
    name: 'socialhub.official',
    username: 'socialhub.official',
    profileType: 'InstagramBusiness',
    isDemo: true
  },
  {
    id: 'demo-wa',
    platformCode: 'whatsapp',
    name: 'WhatsApp Business',
    username: '+92 300 0000000',
    profileType: 'WhatsAppPhone',
    isDemo: true
  },
  {
    id: 'demo-tt',
    platformCode: 'tiktok',
    name: '@socialhub',
    username: 'socialhub',
    profileType: 'TikTokAccount',
    isDemo: true
  },
  {
    id: 'demo-yt',
    platformCode: 'youtube',
    name: 'SocialHub Channel',
    username: '@socialhub',
    profileType: 'YouTubeChannel',
    isDemo: true
  },
  {
    id: 'demo-li',
    platformCode: 'linkedin',
    name: 'SocialHub Company',
    username: 'socialhub',
    profileType: 'LinkedInPage',
    isDemo: true
  },
  {
    id: 'demo-x',
    platformCode: 'twitter',
    name: '@socialhub',
    username: 'socialhub',
    profileType: 'TwitterAccount',
    isDemo: true
  }
];
