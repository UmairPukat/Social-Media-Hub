export const environment = {
  production: true,
  apiUrl: "https://socialbackend-production-a9ea.up.railway.app/api",
  meta: {
    facebook: {
      appId: "YOUR_FACEBOOK_APP_ID",
      redirectUri: "https://YOUR-FRONTEND.up.railway.app/integrations/callback/facebook",
      graphVersion: "v21.0",
      scopes: 'pages_show_list,pages_read_engagement,pages_manage_posts,pages_manage_engagement,pages_messaging'
    },
    instagram: {
      appId: "YOUR_FACEBOOK_APP_ID",
      redirectUri: "https://YOUR-FRONTEND.up.railway.app/integrations/callback/instagram",
      graphVersion: "v21.0",
      scopes: 'pages_show_list,pages_read_engagement,instagram_basic,instagram_content_publish,instagram_manage_comments,instagram_manage_messages,business_management'
    },
    whatsapp: {
      appId: "YOUR_WHATSAPP_APP_ID",
      redirectUri: "https://YOUR-FRONTEND.up.railway.app/integrations/callback/whatsapp",
      graphVersion: "v21.0",
      scopes: 'whatsapp_business_management,whatsapp_business_messaging,business_management'
    }
  }
};
