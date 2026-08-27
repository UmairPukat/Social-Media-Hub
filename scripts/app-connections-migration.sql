-- App Connections: platform rows (MenuType=app_connection) + per-user app config table.
-- Safe to run multiple times.

ALTER TABLE "Platforms" ADD COLUMN IF NOT EXISTS "MenuType" character varying(50) NOT NULL DEFAULT 'integration';
ALTER TABLE "SocialAccounts" ADD COLUMN IF NOT EXISTS "MenuType" character varying(50) NOT NULL DEFAULT 'integration';

UPDATE "Platforms" SET "MenuType" = 'integration' WHERE "MenuType" IS NULL OR TRIM("MenuType") = '';
UPDATE "SocialAccounts" SET "MenuType" = 'integration' WHERE "MenuType" IS NULL OR TRIM("MenuType") = '';

DROP INDEX IF EXISTS "IX_SocialAccounts_UserId_PlatformId";
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SocialAccounts_UserId_PlatformId_MenuType"
    ON "SocialAccounts" ("UserId", "PlatformId", "MenuType");

DROP INDEX IF EXISTS "IX_Platforms_Code";
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Platforms_Code_MenuType"
    ON "Platforms" ("Code", "MenuType");

CREATE TABLE IF NOT EXISTS "AppConnectionConfigs" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "PlatformId" uuid NOT NULL,
    "PlatformCode" character varying(50) NOT NULL,
    "MenuType" character varying(50) NOT NULL DEFAULT 'app_connection',
    "Label" character varying(200) NULL,
    "ClientId" character varying(200) NOT NULL,
    "ClientSecret" text NOT NULL,
    "RedirectUri" character varying(2000) NULL,
    "AuthUrl" character varying(2000) NULL,
    "BaseUrl" character varying(500) NULL,
    "Scopes" character varying(2000) NULL,
    "GraphApiVersion" character varying(20) NOT NULL DEFAULT 'v21.0',
    "WebhookVerifyToken" character varying(500) NULL,
    "PhoneNumberId" character varying(100) NULL,
    "WabaId" character varying(100) NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_AppConnectionConfigs" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_AppConnectionConfigs_UserId_PlatformId_MenuType"
    ON "AppConnectionConfigs" ("UserId", "PlatformId", "MenuType");
