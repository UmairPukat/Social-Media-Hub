/**
 * Writes src/environments/environment.prod.ts from Railway / CI env vars.
 * Safe defaults keep production builds working when vars are missing.
 */
const fs = require('fs');
const path = require('path');

const apiUrl =
  process.env.API_URL ||
  'https://socialbackend-production-a9ea.up.railway.app/api';

const frontendOrigin =
  process.env.FRONTEND_ORIGIN ||
  'https://socialfrontend-production.up.railway.app';

const hubUrl =
  process.env.HUB_URL ||
  apiUrl.replace(/\/api\/?$/, '') + '/hubs/inbox';

const graphVersion = process.env.META_GRAPH_VERSION || 'v21.0';
const facebookAppId = process.env.META_FACEBOOK_APP_ID || '1106538287780623';
const whatsappAppId = process.env.META_WHATSAPP_APP_ID || 'YOUR_WHATSAPP_APP_ID';
const instagramAppId = process.env.META_INSTAGRAM_APP_ID || facebookAppId;

// One Valid OAuth Redirect URI for every Meta product.
const sharedRedirect =
  process.env.META_REDIRECT_URI ||
  process.env.META_FACEBOOK_REDIRECT_URI ||
  `${frontendOrigin}/integrations/callback`;

const content = `export const environment = {
  production: true,
  apiUrl: ${JSON.stringify(apiUrl)},
  hubUrl: ${JSON.stringify(hubUrl)},
  meta: {
    redirectUri: ${JSON.stringify(sharedRedirect)},
    facebook: {
      appId: ${JSON.stringify(facebookAppId)},
      redirectUri: ${JSON.stringify(sharedRedirect)},
      graphVersion: ${JSON.stringify(graphVersion)},
      scopes: 'pages_show_list,pages_read_engagement,pages_read_user_content,pages_manage_metadata,pages_manage_posts,pages_manage_engagement,pages_messaging,business_management'
    },
    instagram: {
      appId: ${JSON.stringify(instagramAppId)},
      redirectUri: ${JSON.stringify(sharedRedirect)},
      graphVersion: ${JSON.stringify(graphVersion)},
      scopes: 'pages_show_list,pages_read_engagement,pages_manage_metadata,pages_messaging,instagram_basic,instagram_content_publish,instagram_manage_comments,instagram_manage_messages,business_management'
    },
    whatsapp: {
      appId: ${JSON.stringify(whatsappAppId)},
      redirectUri: ${JSON.stringify(sharedRedirect)},
      graphVersion: ${JSON.stringify(graphVersion)},
      scopes: 'whatsapp_business_management,whatsapp_business_messaging,business_management'
    }
  }
};
`;

const out = path.join(__dirname, '..', 'src', 'environments', 'environment.prod.ts');
fs.writeFileSync(out, content, 'utf8');
console.log(`[set-env] Wrote ${out}`);
console.log(`[set-env] apiUrl=${apiUrl}`);
console.log(`[set-env] hubUrl=${hubUrl}`);
console.log(`[set-env] redirectUri=${sharedRedirect}`);
