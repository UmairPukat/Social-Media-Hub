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

export const CREATE_PLATFORMS: {
  code: CreatePlatform;
  label: string;
  icon: string;
  color: string;
  supportsPublish: boolean;
  hint: string;
}[] = [
  {
    code: 'facebook',
    label: 'Facebook',
    icon: 'facebook',
    color: PLATFORM_COLORS['facebook'],
    supportsPublish: true,
    hint: 'Page post · text, photo/link'
  },
  {
    code: 'instagram',
    label: 'Instagram',
    icon: 'photo_camera',
    color: PLATFORM_COLORS['instagram'],
    supportsPublish: true,
    hint: 'Feed post · image required'
  },
  {
    code: 'whatsapp',
    label: 'WhatsApp Business',
    icon: 'chat',
    color: PLATFORM_COLORS['whatsapp'],
    supportsPublish: false,
    hint: 'Broadcast / status-style message'
  },
  {
    code: 'tiktok',
    label: 'TikTok',
    icon: 'music_note',
    color: PLATFORM_COLORS['tiktok'],
    supportsPublish: false,
    hint: 'Vertical clip + caption'
  },
  {
    code: 'youtube',
    label: 'YouTube',
    icon: 'smart_display',
    color: PLATFORM_COLORS['youtube'],
    supportsPublish: false,
    hint: 'Video title & description'
  },
  {
    code: 'linkedin',
    label: 'LinkedIn',
    icon: 'work',
    color: PLATFORM_COLORS['linkedin'],
    supportsPublish: false,
    hint: 'Professional update'
  },
  {
    code: 'twitter',
    label: 'X (Twitter)',
    icon: 'tag',
    color: PLATFORM_COLORS['twitter'],
    supportsPublish: false,
    hint: 'Short post / thread'
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
