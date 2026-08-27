-- Adds MenuType to Platforms and SocialAccounts.
-- Safe to run multiple times (IF NOT EXISTS / IF EXISTS guards).
-- Default: integration (Integrations menu). Future: app_connection.

ALTER TABLE "Platforms" ADD COLUMN IF NOT EXISTS "MenuType" character varying(50) NOT NULL DEFAULT 'integration';
ALTER TABLE "SocialAccounts" ADD COLUMN IF NOT EXISTS "MenuType" character varying(50) NOT NULL DEFAULT 'integration';

UPDATE "Platforms" SET "MenuType" = 'integration' WHERE "MenuType" IS NULL OR TRIM("MenuType") = '';
UPDATE "SocialAccounts" SET "MenuType" = 'integration' WHERE "MenuType" IS NULL OR TRIM("MenuType") = '';

DROP INDEX IF EXISTS "IX_SocialAccounts_UserId_PlatformId";
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SocialAccounts_UserId_PlatformId_MenuType"
    ON "SocialAccounts" ("UserId", "PlatformId", "MenuType");

CREATE INDEX IF NOT EXISTS "IX_Platforms_MenuType" ON "Platforms" ("MenuType");
