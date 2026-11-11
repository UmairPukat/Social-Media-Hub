-- =============================================================================
-- Delete inbox / content / webhook / connected-account data for all five modules
-- Social Media Hub — PostgreSQL
--
-- REMOVES (Integrations, App Connections, Developer Apps, Social Media Accounts, Account Connections):
--   WebhookLogs, WebhookEvents
--   Messages, MessageAttachments, Conversations
--   Comments, Media, Posts
--   SyncJobs
--   SocialAuths, SocialProfiles, SocialAccounts
--
-- KEEPS:
--   Users, AccessTokens
--   *Platforms
--   *AppConfigs (client id / secret so Connect can be used again)
--
-- Safe to re-run. Run against your Railway / local Postgres database.
-- =============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. Break self-references (ParentCommentId = ON DELETE RESTRICT)
-- ---------------------------------------------------------------------------
UPDATE "IntegrationComments"         SET "ParentCommentId" = NULL WHERE "ParentCommentId" IS NOT NULL;
UPDATE "AppConnectionComments"       SET "ParentCommentId" = NULL WHERE "ParentCommentId" IS NOT NULL;
UPDATE "DeveloperAppComments"        SET "ParentCommentId" = NULL WHERE "ParentCommentId" IS NOT NULL;
UPDATE "SocialMediaAccountComments"  SET "ParentCommentId" = NULL WHERE "ParentCommentId" IS NOT NULL;
UPDATE "AccountConnectionComments"   SET "ParentCommentId" = NULL WHERE "ParentCommentId" IS NOT NULL;

-- Optional: clear message reply pointers (no FK, but avoids orphans in UI)
UPDATE "IntegrationMessages"         SET "ReplyToMessageId" = NULL WHERE "ReplyToMessageId" IS NOT NULL;
UPDATE "AppConnectionMessages"       SET "ReplyToMessageId" = NULL WHERE "ReplyToMessageId" IS NOT NULL;
UPDATE "DeveloperAppMessages"        SET "ReplyToMessageId" = NULL WHERE "ReplyToMessageId" IS NOT NULL;
UPDATE "SocialMediaAccountMessages"  SET "ReplyToMessageId" = NULL WHERE "ReplyToMessageId" IS NOT NULL;
UPDATE "AccountConnectionMessages"   SET "ReplyToMessageId" = NULL WHERE "ReplyToMessageId" IS NOT NULL;

-- ---------------------------------------------------------------------------
-- 2. Integrations module
-- ---------------------------------------------------------------------------
DELETE FROM "IntegrationMessageAttachments";
DELETE FROM "IntegrationMessages";
DELETE FROM "IntegrationConversations";
DELETE FROM "IntegrationComments";
DELETE FROM "IntegrationMedia";
DELETE FROM "IntegrationPosts";
DELETE FROM "IntegrationSyncJobs";
DELETE FROM "IntegrationWebhookLogs";
DELETE FROM "IntegrationWebhookEvents";

-- ---------------------------------------------------------------------------
-- 3. App Connections module
-- ---------------------------------------------------------------------------
DELETE FROM "AppConnectionMessageAttachments";
DELETE FROM "AppConnectionMessages";
DELETE FROM "AppConnectionConversations";
DELETE FROM "AppConnectionComments";
DELETE FROM "AppConnectionMedia";
DELETE FROM "AppConnectionPosts";
DELETE FROM "AppConnectionSyncJobs";
DELETE FROM "AppConnectionWebhookLogs";
DELETE FROM "AppConnectionWebhookEvents";

-- ---------------------------------------------------------------------------
-- 4. Developer Apps module
-- ---------------------------------------------------------------------------
DELETE FROM "DeveloperAppMessageAttachments";
DELETE FROM "DeveloperAppMessages";
DELETE FROM "DeveloperAppConversations";
DELETE FROM "DeveloperAppComments";
DELETE FROM "DeveloperAppMedia";
DELETE FROM "DeveloperAppPosts";
DELETE FROM "DeveloperAppSyncJobs";
DELETE FROM "DeveloperAppWebhookLogs";
DELETE FROM "DeveloperAppWebhookEvents";

-- ---------------------------------------------------------------------------
-- 5. Social Media Accounts module
-- ---------------------------------------------------------------------------
DELETE FROM "SocialMediaAccountMessageAttachments";
DELETE FROM "SocialMediaAccountMessages";
DELETE FROM "SocialMediaAccountConversations";
DELETE FROM "SocialMediaAccountComments";
DELETE FROM "SocialMediaAccountMedia";
DELETE FROM "SocialMediaAccountPosts";
DELETE FROM "SocialMediaAccountSyncJobs";
DELETE FROM "SocialMediaAccountWebhookLogs";
DELETE FROM "SocialMediaAccountWebhookEvents";

-- ---------------------------------------------------------------------------
-- 6. Account Connections module
-- ---------------------------------------------------------------------------
DELETE FROM "AccountConnectionMessageAttachments";
DELETE FROM "AccountConnectionMessages";
DELETE FROM "AccountConnectionConversations";
DELETE FROM "AccountConnectionComments";
DELETE FROM "AccountConnectionMedia";
DELETE FROM "AccountConnectionPosts";
DELETE FROM "AccountConnectionSyncJobs";
DELETE FROM "AccountConnectionWebhookLogs";
DELETE FROM "AccountConnectionWebhookEvents";

-- ---------------------------------------------------------------------------
-- 7. Connected accounts (content rows above must already be gone)
-- ---------------------------------------------------------------------------
DELETE FROM "IntegrationSocialAuths";
DELETE FROM "IntegrationSocialProfiles";
DELETE FROM "IntegrationSocialAccounts";
DELETE FROM "AppConnectionSocialAuths";
DELETE FROM "AppConnectionSocialProfiles";
DELETE FROM "AppConnectionSocialAccounts";
DELETE FROM "DeveloperAppSocialAuths";
DELETE FROM "DeveloperAppSocialProfiles";
DELETE FROM "DeveloperAppSocialAccounts";
DELETE FROM "SocialMediaAccountSocialAuths";
DELETE FROM "SocialMediaAccountSocialProfiles";
DELETE FROM "SocialMediaAccountSocialAccounts";
DELETE FROM "AccountConnectionSocialAuths";
DELETE FROM "AccountConnectionSocialProfiles";
DELETE FROM "AccountConnectionSocialAccounts";

COMMIT;

-- ---------------------------------------------------------------------------
-- Verify (optional — run after COMMIT)
-- ---------------------------------------------------------------------------
-- SELECT 'IntegrationMessages' AS tbl, COUNT(*) FROM "IntegrationMessages"
-- UNION ALL SELECT 'IntegrationWebhookEvents', COUNT(*) FROM "IntegrationWebhookEvents"
-- UNION ALL SELECT 'IntegrationPlatforms', COUNT(*) FROM "IntegrationPlatforms"
-- UNION ALL SELECT 'IntegrationAppConfigs', COUNT(*) FROM "IntegrationAppConfigs"
-- UNION ALL SELECT 'IntegrationSocialAccounts', COUNT(*) FROM "IntegrationSocialAccounts";

-- Reclaim disk space after a large wipe (optional, may lock tables briefly):
-- VACUUM ANALYZE "IntegrationMessages", "IntegrationWebhookEvents", "IntegrationPosts";
