-- =============================================================================
-- App Connections — full PostgreSQL migration
-- Social Media Hub
--
-- Run once against your Postgres database (Railway, local, etc.).
-- Safe to re-run: uses IF NOT EXISTS / conditional blocks.
--
-- What this adds:
--   1. MetaAppConnections table (per-user Meta app credentials + scopes)
--   2. SocialAccounts.MetaAppConnectionId column + FK
--   3. Filtered unique indexes so Integrations and App Connections can both
--      connect the same platform type without conflicting
-- =============================================================================

BEGIN;

-- -----------------------------------------------------------------------------
-- 1. MetaAppConnections table
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "MetaAppConnections" (
    "Id"                uuid                     NOT NULL,
    "UserId"            uuid                     NOT NULL,
    "Name"              character varying(150)   NOT NULL,
    "PlatformCode"      character varying(50)    NOT NULL,
    "AppId"             character varying(100)   NOT NULL,
    "AppSecret"         character varying(500)   NOT NULL,
    "CallbackUrl"       character varying(2000)  NOT NULL,
    "GraphApiVersion"   character varying(20)    NOT NULL DEFAULT 'v21.0',
    "Scopes"            character varying(2000)  NOT NULL DEFAULT '',
    "CreatedAt"         timestamp with time zone NOT NULL DEFAULT NOW(),
    "UpdatedAt"         timestamp with time zone NULL,
    CONSTRAINT "PK_MetaAppConnections" PRIMARY KEY ("Id")
);

-- FK to Users (skip if already exists)
DO $$ BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_MetaAppConnections_Users_UserId'
    ) THEN
        ALTER TABLE "MetaAppConnections"
            ADD CONSTRAINT "FK_MetaAppConnections_Users_UserId"
            FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_MetaAppConnections_UserId_Name"
    ON "MetaAppConnections" ("UserId", "Name");

-- Column added after initial deploy — safe for databases created before Scopes existed
ALTER TABLE "MetaAppConnections"
    ADD COLUMN IF NOT EXISTS "Scopes" character varying(2000) NOT NULL DEFAULT '';

-- -----------------------------------------------------------------------------
-- 2. SocialAccounts — link to MetaAppConnections
-- -----------------------------------------------------------------------------
ALTER TABLE "SocialAccounts"
    ADD COLUMN IF NOT EXISTS "MetaAppConnectionId" uuid NULL;

DO $$ BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_SocialAccounts_MetaAppConnections_MetaAppConnectionId'
    ) THEN
        ALTER TABLE "SocialAccounts"
            ADD CONSTRAINT "FK_SocialAccounts_MetaAppConnections_MetaAppConnectionId"
            FOREIGN KEY ("MetaAppConnectionId")
            REFERENCES "MetaAppConnections" ("Id")
            ON DELETE SET NULL;
    END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 3. Unique indexes — allow one Integrations account + multiple App Connection
--    accounts per platform per user
-- -----------------------------------------------------------------------------

-- Drop legacy single unique index if present (blocks multiple app connections)
DROP INDEX IF EXISTS "IX_SocialAccounts_UserId_PlatformId";

CREATE UNIQUE INDEX IF NOT EXISTS "IX_SocialAccounts_UserId_PlatformId"
    ON "SocialAccounts" ("UserId", "PlatformId")
    WHERE "MetaAppConnectionId" IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_SocialAccounts_UserId_PlatformId_MetaAppConnectionId"
    ON "SocialAccounts" ("UserId", "PlatformId", "MetaAppConnectionId")
    WHERE "MetaAppConnectionId" IS NOT NULL;

COMMIT;

-- -----------------------------------------------------------------------------
-- Verification (optional — run manually after migration)
-- -----------------------------------------------------------------------------
-- SELECT table_name FROM information_schema.tables WHERE table_name = 'MetaAppConnections';
-- SELECT column_name FROM information_schema.columns
--   WHERE table_name = 'SocialAccounts' AND column_name = 'MetaAppConnectionId';
-- SELECT indexname, indexdef FROM pg_indexes
--   WHERE tablename = 'SocialAccounts' AND indexname LIKE 'IX_SocialAccounts%';

-- =============================================================================
-- Default OAuth scopes (stored per row in MetaAppConnections.Scopes; applied in UI)
-- =============================================================================
-- facebook:
--   public_profile,email,pages_show_list,pages_read_engagement,pages_read_user_content,
--   pages_manage_posts,pages_manage_engagement,pages_manage_metadata,pages_messaging,business_management
--
-- instagram:
--   public_profile,email,pages_show_list,pages_read_engagement,pages_read_user_content,
--   pages_manage_metadata,pages_messaging,business_management,instagram_basic,instagram_manage_comments,
--   instagram_manage_messages,instagram_content_publish
--
-- instagram_login:
--   instagram_business_basic,instagram_business_content_publish,instagram_business_manage_messages,
--   instagram_business_manage_comments
--
-- whatsapp:
--   whatsapp_business_management,whatsapp_business_messaging,business_management
-- =============================================================================

-- =============================================================================
-- ROLLBACK (manual — only if you need to undo App Connections entirely)
-- =============================================================================
-- BEGIN;
-- DROP INDEX IF EXISTS "IX_SocialAccounts_UserId_PlatformId_MetaAppConnectionId";
-- ALTER TABLE "SocialAccounts" DROP CONSTRAINT IF EXISTS "FK_SocialAccounts_MetaAppConnections_MetaAppConnectionId";
-- ALTER TABLE "SocialAccounts" DROP COLUMN IF EXISTS "MetaAppConnectionId";
-- CREATE UNIQUE INDEX IF NOT EXISTS "IX_SocialAccounts_UserId_PlatformId"
--     ON "SocialAccounts" ("UserId", "PlatformId");
-- DROP TABLE IF EXISTS "MetaAppConnections";
-- COMMIT;
-- =============================================================================
