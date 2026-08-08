export const environment = {
  production: false,
  apiUrl: 'https://socialbackend-production-a9ea.up.railway.app/api',
  hubUrl: 'https://socialbackend-production-a9ea.up.railway.app/hubs/inbox',
  // Frontend builds Meta auth URLs — backend only stores tokens after OAuth.
  meta: {
    // One Valid OAuth Redirect URI for Facebook, Instagram, and WhatsApp.
    redirectUri: 'http://localhost:4200/integrations/callback',
    facebook: {
      appId: '1106538287780623',
      redirectUri: 'http://localhost:4200/integrations/callback',
      graphVersion: 'v21.0',
      // pages_read_user_content reads visitor comments; pages_manage_metadata subscribes page webhooks.
      scopes: 'pages_show_list,pages_read_engagement,pages_read_user_content,pages_manage_metadata,pages_manage_posts,pages_manage_engagement,pages_messaging,business_management'
    },
    instagram: {
      // Same Meta App Id as Facebook — Instagram uses Facebook Login.
      appId: '1106538287780623',
      redirectUri: 'http://localhost:4200/integrations/callback',
      graphVersion: 'v21.0',
      scopes: 'pages_show_list,pages_read_engagement,pages_manage_metadata,pages_messaging,instagram_basic,instagram_content_publish,instagram_manage_comments,instagram_manage_messages,business_management'
    },
    whatsapp: {
      appId: 'YOUR_WHATSAPP_APP_ID',
      redirectUri: 'http://localhost:4200/integrations/callback',
      graphVersion: 'v21.0',
      scopes: 'whatsapp_business_management,whatsapp_business_messaging,business_management'
    }
  }
};
