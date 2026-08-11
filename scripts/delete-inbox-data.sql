-- Wipe posts, comments, messages, and webhook logs.
-- Safe for PostgreSQL; respects FK order (including comment replies / message replies).

BEGIN;

-- Comment replies reference parent comments (OnDelete Restrict).
UPDATE "Comments"
SET "ParentCommentId" = NULL
WHERE "ParentCommentId" IS NOT NULL;

-- Message replies reference other messages (added later via seeder).
UPDATE "Messages"
SET "ReplyToMessageId" = NULL
WHERE "ReplyToMessageId" IS NOT NULL;

-- Child rows first
DELETE FROM "MessageAttachments";
DELETE FROM "Messages";
DELETE FROM "Comments";
DELETE FROM "Media";
DELETE FROM "Posts";

-- Logs
DELETE FROM "WebhookLogs";
DELETE FROM "WebhookEvents";

COMMIT;

-- Optional: reclaim space after a large wipe
-- VACUUM ANALYZE "Posts", "Comments", "Messages", "Media", "MessageAttachments", "WebhookLogs", "WebhookEvents";
