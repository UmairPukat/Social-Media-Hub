-- =============================================================================
-- Delete inbox / content / webhook data for all three modules
-- Social Media Hub — PostgreSQL
--
-- REMOVES (Integrations, App Connections, Developer Apps):
--   WebhookLogs, WebhookEvents
--   Messages, MessageAttachments, Conversations
--   Comments, Media, Posts
--   SyncJobs
--
-- KEEPS:
--   Users, AccessTokens
--   *Platforms (IntegrationPlatforms, AppConnectionPlatforms, DeveloperAppPlatforms)
--   *AppConfigs (IntegrationAppConfigs, AppConnectionConfigs, DeveloperAppConfigs)
--   SocialAccounts, SocialAuths, SocialProfiles (connected accounts & tokens)
--
-- Safe to re-run. Run against your Railway / local Postgres database.
-- =============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. Break self-references (ParentCommentId = ON DELETE RESTRICT)
-- ---------------------------------------------------------------------------
UPDATE "IntegrationComments"      SET "ParentCommentId" = NULL WHERE "ParentCommentId" IS NOT NULL;
UPDATE "AppConnectionComments"    SET "ParentCommentId" = NULL WHERE "ParentCommentId" IS NOT NULL;
UPDATE "DeveloperAppComments"     SET "ParentCommentId" = NULL WHERE "ParentCommentId" IS NOT NULL;

-- Optional: clear message reply pointers (no FK, but avoids orphans in UI)
UPDATE "IntegrationMessages"      SET "ReplyToMessageId" = NULL WHERE "ReplyToMessageId" IS NOT NULL;
UPDATE "AppConnectionMessages"    SET "ReplyToMessageId" = NULL WHERE "ReplyToMessageId" IS NOT NULL;
UPDATE "DeveloperAppMessages"     SET "ReplyToMessageId" = NULL WHERE "ReplyToMessageId" IS NOT NULL;

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
