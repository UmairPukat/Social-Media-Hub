/**
 * Writes src/environments/environment.prod.ts from Railway / CI env vars.
 * Safe defaults keep production builds working when vars are missing.
 * Auth URLs and the OAuth redirect are owned by the backend
 * (Integrations/BeginOAuth → /api/Integrations/Callback).
 */
const fs = require('fs');
const path = require('path');

const apiUrl =
  process.env.API_URL ||
  'https://socialbackend-production-a9ea.up.railway.app/api';

const hubUrl =
  process.env.HUB_URL ||
  apiUrl.replace(/\/api\/?$/, '') + '/hubs/inbox';

const facebookAppId = process.env.META_FACEBOOK_APP_ID || '1106538287780623';
const whatsappAppId = process.env.META_WHATSAPP_APP_ID || 'YOUR_WHATSAPP_APP_ID';
const instagramAppId = process.env.META_INSTAGRAM_APP_ID || facebookAppId;
const instagramLoginAppId = process.env.META_INSTAGRAM_LOGIN_APP_ID || 'YOUR_INSTAGRAM_LOGIN_APP_ID';

const youtubeClientId = process.env.YOUTUBE_CLIENT_ID || 'YOUR_YOUTUBE_CLIENT_ID';
const tiktokClientKey = process.env.TIKTOK_CLIENT_KEY || 'YOUR_TIKTOK_CLIENT_KEY';

const content = `export const environment = {
  production: true,
  apiUrl: ${JSON.stringify(apiUrl)},
  hubUrl: ${JSON.stringify(hubUrl)},
  meta: {
    facebook: { appId: ${JSON.stringify(facebookAppId)} },
    instagram: { appId: ${JSON.stringify(instagramAppId)} },
    instagram_login: { appId: ${JSON.stringify(instagramLoginAppId)} },
    whatsapp: { appId: ${JSON.stringify(whatsappAppId)} },
    youtube: { appId: ${JSON.stringify(youtubeClientId)} },
    tiktok: { appId: ${JSON.stringify(tiktokClientKey)} }
  }
};
`;

const out = path.join(__dirname, '..', 'src', 'environments', 'environment.prod.ts');
fs.writeFileSync(out, content, 'utf8');
console.log(`[set-env] Wrote ${out}`);
console.log(`[set-env] apiUrl=${apiUrl}`);
console.log(`[set-env] hubUrl=${hubUrl}`);
