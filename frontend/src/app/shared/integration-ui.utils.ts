export function integrationTone(code: string): string {
  const map: Record<string, string> = {
    facebook: 'blue',
    instagram: 'rose',
    instagram_login: 'rose',
    threads: 'slate',
    twitter: 'slate',
    x: 'slate',
    linkedin: 'cyan',
    tiktok: 'slate',
    youtube: 'rose',
    pinterest: 'rose',
    reddit: 'orange',
    snapchat: 'amber',
    whatsapp: 'green',
    outlook: 'blue',
    gmail: 'rose',
    microsoft365: 'blue',
    exchange: 'blue',
    telegram: 'cyan',
    discord: 'violet',
    slack: 'violet',
    teams: 'violet',
    shopify: 'green',
    woocommerce: 'violet',
    tiktokshop: 'pink',
    amazon: 'amber',
    etsy: 'orange',
    ebay: 'blue',
    salesforce: 'cyan',
    hubspot: 'orange',
    zoho: 'teal',
    dynamics365: 'blue',
    pipedrive: 'green',
    googlecalendar: 'blue',
    outlookcalendar: 'cyan',
    applecalendar: 'slate',
    onedrive: 'blue',
    googledrive: 'amber',
    dropbox: 'blue',
    sharepoint: 'teal',
    stripe: 'violet',
    paypal: 'blue',
    square: 'slate',
    openai: 'teal',
    azureopenai: 'blue',
    claude: 'orange',
    gemini: 'pink'
  };
  return map[code.toLowerCase()] || 'slate';
}

export function integrationPlatformIcon(code: string): string {
  const map: Record<string, string> = {
    facebook: 'public',
    instagram: 'photo_camera',
    instagram_login: 'camera_alt',
    threads: 'tag',
    twitter: 'alternate_email',
    x: 'alternate_email',
    linkedin: 'work',
    tiktok: 'music_note',
    youtube: 'play_circle',
    pinterest: 'push_pin',
    reddit: 'forum',
    snapchat: 'photo_camera_front',
    whatsapp: 'chat',
    outlook: 'mail',
    gmail: 'mail_outline',
    microsoft365: 'apps',
    exchange: 'inbox',
    telegram: 'send',
    discord: 'sports_esports',
    slack: 'tag',
    teams: 'groups',
    shopify: 'storefront',
    woocommerce: 'shopping_bag',
    tiktokshop: 'store',
    amazon: 'local_shipping',
    etsy: 'store',
    ebay: 'gavel',
    salesforce: 'cloud',
    hubspot: 'hub',
    zoho: 'business',
    dynamics365: 'apartment',
    pipedrive: 'trending_up',
    googlecalendar: 'calendar_month',
    outlookcalendar: 'event',
    applecalendar: 'event_available',
    onedrive: 'cloud_queue',
    googledrive: 'add_to_drive',
    dropbox: 'folder',
    sharepoint: 'folder_shared',
    stripe: 'credit_card',
    paypal: 'account_balance_wallet',
    square: 'payments',
    openai: 'auto_awesome',
    azureopenai: 'cloud',
    claude: 'psychology',
    gemini: 'auto_awesome'
  };
  return map[code.toLowerCase()] || 'extension';
}

export function integrationShortDesc(text: string): string {
  const clean = (text || '').trim();
  if (clean.length <= 78) return clean;
  return `${clean.slice(0, 76)}…`;
}

export function supportsIntegrationPageSelection(code: string): boolean {
  return ['facebook', 'instagram'].includes(code.toLowerCase());
}

export function supportsIntegrationConnectionDetails(code: string): boolean {
  return ['facebook', 'instagram', 'instagram_login'].includes(code.toLowerCase());
}
