export const environment = {
  production: false,
  apiUrl: 'https://socialbackend-production-a9ea.up.railway.app/api',
  hubUrl: 'https://socialbackend-production-a9ea.up.railway.app/hubs/inbox',
  // Auth URLs are built by the backend (Integrations/BeginOAuth). Meta's OAuth
  // redirect goes to the backend: /api/Integrations/Callback.
  // App ids are only used to show "not configured" hints in the UI.
  meta: {
    facebook: { appId: '1106538287780623' },
    instagram: { appId: '1106538287780623' },
    instagram_login: { appId: '1038582391966862' },
    whatsapp: { appId: 'YOUR_WHATSAPP_APP_ID' },
    youtube: { appId: 'YOUR_YOUTUBE_CLIENT_ID' },
    tiktok: { appId: 'YOUR_TIKTOK_CLIENT_KEY' }
  }
};
