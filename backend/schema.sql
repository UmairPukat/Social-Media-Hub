CREATE TABLE "AccessTokens" (
    "Id" uuid NOT NULL,
    "Token" character varying(200) NOT NULL,
    "Label" text,
    "IsUsed" boolean NOT NULL,
    "UsedByUserId" uuid,
    "ExpiresAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_AccessTokens" PRIMARY KEY ("Id")
);


CREATE TABLE "AppConnectionPlatforms" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "Name" character varying(100) NOT NULL,
    "Code" character varying(50) NOT NULL,
    "Icon" character varying(500),
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_AppConnectionPlatforms" PRIMARY KEY ("Id")
);


CREATE TABLE "DeveloperAppPlatforms" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "Name" character varying(100) NOT NULL,
    "Code" character varying(50) NOT NULL,
    "Icon" character varying(500),
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_DeveloperAppPlatforms" PRIMARY KEY ("Id")
);


CREATE TABLE "IntegrationPlatforms" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "Name" character varying(100) NOT NULL,
    "Code" character varying(50) NOT NULL,
    "Icon" character varying(500),
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_IntegrationPlatforms" PRIMARY KEY ("Id")
);


CREATE TABLE "Users" (
    "Id" uuid NOT NULL,
    "Email" character varying(256) NOT NULL,
    "PasswordHash" character varying(500) NOT NULL,
    "FullName" character varying(150) NOT NULL,
    "Role" character varying(50) NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
);


CREATE TABLE "AppConnectionWebhookEvents" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "PlatformId" uuid,
    "EventType" character varying(100) NOT NULL,
    "ObjectType" text,
    "ExternalObjectId" text,
    "HeadersJson" text,
    "PayloadJson" text NOT NULL,
    "Signature" text,
    "Status" integer NOT NULL,
    "RetryCount" integer NOT NULL,
    "ReceivedAt" timestamp with time zone NOT NULL,
    "ProcessedAt" timestamp with time zone,
    "Error" text,
    CONSTRAINT "PK_AppConnectionWebhookEvents" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppConnectionWebhookEvents_AppConnectionPlatforms_PlatformId" FOREIGN KEY ("PlatformId") REFERENCES "AppConnectionPlatforms" ("Id") ON DELETE SET NULL
);


CREATE TABLE "AppConnectionWebhookLogs" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "PlatformId" uuid,
    "PlatformCode" character varying(50) NOT NULL,
    "Signature" text,
    "HeadersJson" text,
    "PayloadJson" text NOT NULL,
    "ReceivedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AppConnectionWebhookLogs" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppConnectionWebhookLogs_AppConnectionPlatforms_PlatformId" FOREIGN KEY ("PlatformId") REFERENCES "AppConnectionPlatforms" ("Id") ON DELETE SET NULL
);


CREATE TABLE "DeveloperAppWebhookEvents" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "PlatformId" uuid,
    "EventType" character varying(100) NOT NULL,
    "ObjectType" text,
    "ExternalObjectId" text,
    "HeadersJson" text,
    "PayloadJson" text NOT NULL,
    "Signature" text,
    "Status" integer NOT NULL,
    "RetryCount" integer NOT NULL,
    "ReceivedAt" timestamp with time zone NOT NULL,
    "ProcessedAt" timestamp with time zone,
    "Error" text,
    CONSTRAINT "PK_DeveloperAppWebhookEvents" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_DeveloperAppWebhookEvents_DeveloperAppPlatforms_PlatformId" FOREIGN KEY ("PlatformId") REFERENCES "DeveloperAppPlatforms" ("Id") ON DELETE SET NULL
);


CREATE TABLE "DeveloperAppWebhookLogs" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "PlatformId" uuid,
    "PlatformCode" character varying(50) NOT NULL,
    "Signature" text,
    "HeadersJson" text,
    "PayloadJson" text NOT NULL,
    "ReceivedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_DeveloperAppWebhookLogs" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_DeveloperAppWebhookLogs_DeveloperAppPlatforms_PlatformId" FOREIGN KEY ("PlatformId") REFERENCES "DeveloperAppPlatforms" ("Id") ON DELETE SET NULL
);


CREATE TABLE "IntegrationWebhookEvents" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "PlatformId" uuid,
    "EventType" character varying(100) NOT NULL,
    "ObjectType" text,
    "ExternalObjectId" text,
    "HeadersJson" text,
    "PayloadJson" text NOT NULL,
    "Signature" text,
    "Status" integer NOT NULL,
    "RetryCount" integer NOT NULL,
    "ReceivedAt" timestamp with time zone NOT NULL,
    "ProcessedAt" timestamp with time zone,
    "Error" text,
    CONSTRAINT "PK_IntegrationWebhookEvents" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_IntegrationWebhookEvents_IntegrationPlatforms_PlatformId" FOREIGN KEY ("PlatformId") REFERENCES "IntegrationPlatforms" ("Id") ON DELETE SET NULL
);


CREATE TABLE "IntegrationWebhookLogs" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "PlatformId" uuid,
    "PlatformCode" character varying(50) NOT NULL,
    "Signature" text,
    "HeadersJson" text,
    "PayloadJson" text NOT NULL,
    "ReceivedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_IntegrationWebhookLogs" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_IntegrationWebhookLogs_IntegrationPlatforms_PlatformId" FOREIGN KEY ("PlatformId") REFERENCES "IntegrationPlatforms" ("Id") ON DELETE SET NULL
);


CREATE TABLE "AppConnectionConfigs" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "PlatformId" uuid NOT NULL,
    "PlatformCode" character varying(50) NOT NULL,
    "MenuType" character varying(50) NOT NULL DEFAULT 'app_connection',
    "Label" character varying(200),
    "ClientId" character varying(200) NOT NULL,
    "ClientSecret" text NOT NULL,
    "RedirectUri" character varying(2000),
    "AuthUrl" character varying(2000),
    "BaseUrl" character varying(500),
    "Scopes" character varying(2000),
    "GraphApiVersion" character varying(20) NOT NULL DEFAULT 'v21.0',
    "WebhookVerifyToken" character varying(500),
    "PhoneNumberId" character varying(100),
    "WabaId" character varying(100),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_AppConnectionConfigs" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppConnectionConfigs_AppConnectionPlatforms_PlatformId" FOREIGN KEY ("PlatformId") REFERENCES "AppConnectionPlatforms" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AppConnectionConfigs_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AppConnectionSocialAccounts" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "UserId" uuid NOT NULL,
    "PlatformId" uuid NOT NULL,
    "ExternalAccountId" character varying(200) NOT NULL,
    "DisplayName" character varying(200) NOT NULL,
    "Username" character varying(200),
    "Email" character varying(256),
    "ProfileImage" text,
    "Status" integer NOT NULL,
    "ConnectedAt" timestamp with time zone,
    "LastSyncAt" timestamp with time zone,
    "MetadataJson" text,
    CONSTRAINT "PK_AppConnectionSocialAccounts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppConnectionSocialAccounts_AppConnectionPlatforms_Platform~" FOREIGN KEY ("PlatformId") REFERENCES "AppConnectionPlatforms" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppConnectionSocialAccounts_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "DeveloperAppConfigs" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "PlatformId" uuid NOT NULL,
    "PlatformCode" character varying(50) NOT NULL,
    "MenuType" character varying(50) NOT NULL DEFAULT 'developer_app',
    "Label" text,
    "ClientId" character varying(200) NOT NULL,
    "ClientSecret" text NOT NULL,
    "RedirectUri" text,
    "AuthUrl" text,
    "BaseUrl" text,
    "Scopes" text,
    "GraphApiVersion" text NOT NULL,
    "WebhookVerifyToken" text,
    "PhoneNumberId" text,
    "WabaId" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_DeveloperAppConfigs" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_DeveloperAppConfigs_DeveloperAppPlatforms_PlatformId" FOREIGN KEY ("PlatformId") REFERENCES "DeveloperAppPlatforms" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_DeveloperAppConfigs_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "DeveloperAppSocialAccounts" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "UserId" uuid NOT NULL,
    "PlatformId" uuid NOT NULL,
    "ExternalAccountId" character varying(200) NOT NULL,
    "DisplayName" character varying(200) NOT NULL,
    "Username" character varying(200),
    "Email" character varying(256),
    "ProfileImage" text,
    "Status" integer NOT NULL,
    "ConnectedAt" timestamp with time zone,
    "LastSyncAt" timestamp with time zone,
    "MetadataJson" text,
    CONSTRAINT "PK_DeveloperAppSocialAccounts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_DeveloperAppSocialAccounts_DeveloperAppPlatforms_PlatformId" FOREIGN KEY ("PlatformId") REFERENCES "DeveloperAppPlatforms" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DeveloperAppSocialAccounts_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "IntegrationAppConfigs" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "PlatformId" uuid NOT NULL,
    "PlatformCode" character varying(50) NOT NULL,
    "MenuType" character varying(50) NOT NULL DEFAULT 'integration',
    "Label" text,
    "ClientId" character varying(200) NOT NULL,
    "ClientSecret" text NOT NULL,
    "RedirectUri" text,
    "AuthUrl" text,
    "BaseUrl" text,
    "Scopes" text,
    "GraphApiVersion" text NOT NULL,
    "WebhookVerifyToken" text,
    "PhoneNumberId" text,
    "WabaId" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_IntegrationAppConfigs" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_IntegrationAppConfigs_IntegrationPlatforms_PlatformId" FOREIGN KEY ("PlatformId") REFERENCES "IntegrationPlatforms" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_IntegrationAppConfigs_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "IntegrationSocialAccounts" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "UserId" uuid NOT NULL,
    "PlatformId" uuid NOT NULL,
    "ExternalAccountId" character varying(200) NOT NULL,
    "DisplayName" character varying(200) NOT NULL,
    "Username" character varying(200),
    "Email" character varying(256),
    "ProfileImage" text,
    "Status" integer NOT NULL,
    "ConnectedAt" timestamp with time zone,
    "LastSyncAt" timestamp with time zone,
    "MetadataJson" text,
    CONSTRAINT "PK_IntegrationSocialAccounts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_IntegrationSocialAccounts_IntegrationPlatforms_PlatformId" FOREIGN KEY ("PlatformId") REFERENCES "IntegrationPlatforms" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_IntegrationSocialAccounts_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AppConnectionSocialAuths" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "SocialAccountId" uuid NOT NULL,
    "AccessToken" text NOT NULL,
    "RefreshToken" text,
    "ExpiresAt" timestamp with time zone,
    "Scopes" character varying(1000),
    "WebhookSecret" text,
    CONSTRAINT "PK_AppConnectionSocialAuths" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppConnectionSocialAuths_AppConnectionSocialAccounts_Social~" FOREIGN KEY ("SocialAccountId") REFERENCES "AppConnectionSocialAccounts" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AppConnectionSocialProfiles" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "SocialAccountId" uuid NOT NULL,
    "ExternalProfileId" character varying(200) NOT NULL,
    "ProfileType" integer NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Username" text,
    "ProfileImage" text,
    "MetadataJson" text,
    CONSTRAINT "PK_AppConnectionSocialProfiles" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppConnectionSocialProfiles_AppConnectionSocialAccounts_Soc~" FOREIGN KEY ("SocialAccountId") REFERENCES "AppConnectionSocialAccounts" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AppConnectionSyncJobs" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "SocialAccountId" uuid NOT NULL,
    "EntityType" integer NOT NULL,
    "Cursor" text,
    "StartedAt" timestamp with time zone,
    "FinishedAt" timestamp with time zone,
    "Status" integer NOT NULL,
    "RecordsFetched" integer NOT NULL,
    "Error" text,
    CONSTRAINT "PK_AppConnectionSyncJobs" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppConnectionSyncJobs_AppConnectionSocialAccounts_SocialAcc~" FOREIGN KEY ("SocialAccountId") REFERENCES "AppConnectionSocialAccounts" ("Id") ON DELETE CASCADE
);


CREATE TABLE "DeveloperAppSocialAuths" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "SocialAccountId" uuid NOT NULL,
    "AccessToken" text NOT NULL,
    "RefreshToken" text,
    "ExpiresAt" timestamp with time zone,
    "Scopes" character varying(1000),
    "WebhookSecret" text,
    CONSTRAINT "PK_DeveloperAppSocialAuths" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_DeveloperAppSocialAuths_DeveloperAppSocialAccounts_SocialAc~" FOREIGN KEY ("SocialAccountId") REFERENCES "DeveloperAppSocialAccounts" ("Id") ON DELETE CASCADE
);


CREATE TABLE "DeveloperAppSocialProfiles" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "SocialAccountId" uuid NOT NULL,
    "ExternalProfileId" character varying(200) NOT NULL,
    "ProfileType" integer NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Username" text,
    "ProfileImage" text,
    "MetadataJson" text,
    CONSTRAINT "PK_DeveloperAppSocialProfiles" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_DeveloperAppSocialProfiles_DeveloperAppSocialAccounts_Socia~" FOREIGN KEY ("SocialAccountId") REFERENCES "DeveloperAppSocialAccounts" ("Id") ON DELETE CASCADE
);


CREATE TABLE "DeveloperAppSyncJobs" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "SocialAccountId" uuid NOT NULL,
    "EntityType" integer NOT NULL,
    "Cursor" text,
    "StartedAt" timestamp with time zone,
    "FinishedAt" timestamp with time zone,
    "Status" integer NOT NULL,
    "RecordsFetched" integer NOT NULL,
    "Error" text,
    CONSTRAINT "PK_DeveloperAppSyncJobs" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_DeveloperAppSyncJobs_DeveloperAppSocialAccounts_SocialAccou~" FOREIGN KEY ("SocialAccountId") REFERENCES "DeveloperAppSocialAccounts" ("Id") ON DELETE CASCADE
);


CREATE TABLE "IntegrationSocialAuths" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "SocialAccountId" uuid NOT NULL,
    "AccessToken" text NOT NULL,
    "RefreshToken" text,
    "ExpiresAt" timestamp with time zone,
    "Scopes" character varying(1000),
    "WebhookSecret" text,
    CONSTRAINT "PK_IntegrationSocialAuths" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_IntegrationSocialAuths_IntegrationSocialAccounts_SocialAcco~" FOREIGN KEY ("SocialAccountId") REFERENCES "IntegrationSocialAccounts" ("Id") ON DELETE CASCADE
);


CREATE TABLE "IntegrationSocialProfiles" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "SocialAccountId" uuid NOT NULL,
    "ExternalProfileId" character varying(200) NOT NULL,
    "ProfileType" integer NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Username" text,
    "ProfileImage" text,
    "MetadataJson" text,
    CONSTRAINT "PK_IntegrationSocialProfiles" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_IntegrationSocialProfiles_IntegrationSocialAccounts_SocialA~" FOREIGN KEY ("SocialAccountId") REFERENCES "IntegrationSocialAccounts" ("Id") ON DELETE CASCADE
);


CREATE TABLE "IntegrationSyncJobs" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "SocialAccountId" uuid NOT NULL,
    "EntityType" integer NOT NULL,
    "Cursor" text,
    "StartedAt" timestamp with time zone,
    "FinishedAt" timestamp with time zone,
    "Status" integer NOT NULL,
    "RecordsFetched" integer NOT NULL,
    "Error" text,
    CONSTRAINT "PK_IntegrationSyncJobs" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_IntegrationSyncJobs_IntegrationSocialAccounts_SocialAccount~" FOREIGN KEY ("SocialAccountId") REFERENCES "IntegrationSocialAccounts" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AppConnectionConversations" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "SocialProfileId" uuid NOT NULL,
    "ExternalConversationId" character varying(200) NOT NULL,
    "CustomerId" text,
    "CustomerName" text,
    "CustomerImage" text,
    "UnreadCount" integer NOT NULL,
    "LastMessageAt" timestamp with time zone,
    "Status" integer NOT NULL,
    CONSTRAINT "PK_AppConnectionConversations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppConnectionConversations_AppConnectionSocialProfiles_Soci~" FOREIGN KEY ("SocialProfileId") REFERENCES "AppConnectionSocialProfiles" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AppConnectionPosts" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "SocialProfileId" uuid NOT NULL,
    "PlatformId" uuid NOT NULL,
    "ExternalPostId" character varying(200),
    "Text" character varying(5000),
    "Caption" character varying(5000),
    "Type" integer NOT NULL,
    "Status" integer NOT NULL,
    "LikeCount" integer NOT NULL,
    "CommentCount" integer NOT NULL,
    "ShareCount" integer NOT NULL,
    "ViewCount" integer NOT NULL,
    "PublishedAt" timestamp with time zone,
    "MetadataJson" text,
    "ErrorMessage" text,
    CONSTRAINT "PK_AppConnectionPosts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppConnectionPosts_AppConnectionPlatforms_PlatformId" FOREIGN KEY ("PlatformId") REFERENCES "AppConnectionPlatforms" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppConnectionPosts_AppConnectionSocialProfiles_SocialProfil~" FOREIGN KEY ("SocialProfileId") REFERENCES "AppConnectionSocialProfiles" ("Id") ON DELETE CASCADE
);


CREATE TABLE "DeveloperAppConversations" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "SocialProfileId" uuid NOT NULL,
    "ExternalConversationId" character varying(200) NOT NULL,
    "CustomerId" text,
    "CustomerName" text,
    "CustomerImage" text,
    "UnreadCount" integer NOT NULL,
    "LastMessageAt" timestamp with time zone,
    "Status" integer NOT NULL,
    CONSTRAINT "PK_DeveloperAppConversations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_DeveloperAppConversations_DeveloperAppSocialProfiles_Social~" FOREIGN KEY ("SocialProfileId") REFERENCES "DeveloperAppSocialProfiles" ("Id") ON DELETE CASCADE
);


CREATE TABLE "DeveloperAppPosts" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "SocialProfileId" uuid NOT NULL,
    "PlatformId" uuid NOT NULL,
    "ExternalPostId" character varying(200),
    "Text" character varying(5000),
    "Caption" character varying(5000),
    "Type" integer NOT NULL,
    "Status" integer NOT NULL,
    "LikeCount" integer NOT NULL,
    "CommentCount" integer NOT NULL,
    "ShareCount" integer NOT NULL,
    "ViewCount" integer NOT NULL,
    "PublishedAt" timestamp with time zone,
    "MetadataJson" text,
    "ErrorMessage" text,
    CONSTRAINT "PK_DeveloperAppPosts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_DeveloperAppPosts_DeveloperAppPlatforms_PlatformId" FOREIGN KEY ("PlatformId") REFERENCES "DeveloperAppPlatforms" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DeveloperAppPosts_DeveloperAppSocialProfiles_SocialProfileId" FOREIGN KEY ("SocialProfileId") REFERENCES "DeveloperAppSocialProfiles" ("Id") ON DELETE CASCADE
);


CREATE TABLE "IntegrationConversations" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "SocialProfileId" uuid NOT NULL,
    "ExternalConversationId" character varying(200) NOT NULL,
    "CustomerId" text,
    "CustomerName" text,
    "CustomerImage" text,
    "UnreadCount" integer NOT NULL,
    "LastMessageAt" timestamp with time zone,
    "Status" integer NOT NULL,
    CONSTRAINT "PK_IntegrationConversations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_IntegrationConversations_IntegrationSocialProfiles_SocialPr~" FOREIGN KEY ("SocialProfileId") REFERENCES "IntegrationSocialProfiles" ("Id") ON DELETE CASCADE
);


CREATE TABLE "IntegrationPosts" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "SocialProfileId" uuid NOT NULL,
    "PlatformId" uuid NOT NULL,
    "ExternalPostId" character varying(200),
    "Text" character varying(5000),
    "Caption" character varying(5000),
    "Type" integer NOT NULL,
    "Status" integer NOT NULL,
    "LikeCount" integer NOT NULL,
    "CommentCount" integer NOT NULL,
    "ShareCount" integer NOT NULL,
    "ViewCount" integer NOT NULL,
    "PublishedAt" timestamp with time zone,
    "MetadataJson" text,
    "ErrorMessage" text,
    CONSTRAINT "PK_IntegrationPosts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_IntegrationPosts_IntegrationPlatforms_PlatformId" FOREIGN KEY ("PlatformId") REFERENCES "IntegrationPlatforms" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_IntegrationPosts_IntegrationSocialProfiles_SocialProfileId" FOREIGN KEY ("SocialProfileId") REFERENCES "IntegrationSocialProfiles" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AppConnectionMessages" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "ConversationId" uuid NOT NULL,
    "ExternalMessageId" character varying(200) NOT NULL,
    "SenderId" text,
    "ReceiverId" text,
    "Direction" integer NOT NULL,
    "MessageType" integer NOT NULL,
    "Body" character varying(5000),
    "Status" integer NOT NULL,
    "PlatformCreatedAt" timestamp with time zone,
    "ReplyToMessageId" uuid,
    "ReplyToExternalId" character varying(200),
    CONSTRAINT "PK_AppConnectionMessages" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppConnectionMessages_AppConnectionConversations_Conversati~" FOREIGN KEY ("ConversationId") REFERENCES "AppConnectionConversations" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AppConnectionComments" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "PostId" uuid NOT NULL,
    "ParentCommentId" uuid,
    "ExternalCommentId" character varying(200) NOT NULL,
    "AuthorId" text,
    "AuthorName" character varying(200) NOT NULL,
    "AuthorImage" text,
    "Message" character varying(5000) NOT NULL,
    "LikeCount" integer NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "IsHidden" boolean NOT NULL,
    "PlatformCreatedAt" timestamp with time zone,
    CONSTRAINT "PK_AppConnectionComments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppConnectionComments_AppConnectionComments_ParentCommentId" FOREIGN KEY ("ParentCommentId") REFERENCES "AppConnectionComments" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppConnectionComments_AppConnectionPosts_PostId" FOREIGN KEY ("PostId") REFERENCES "AppConnectionPosts" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AppConnectionMedia" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "PostId" uuid NOT NULL,
    "ExternalMediaId" text,
    "MediaType" integer NOT NULL,
    "Url" character varying(2000) NOT NULL,
    "Thumbnail" text,
    "Width" integer,
    "Height" integer,
    "Duration" integer,
    "DisplayOrder" integer NOT NULL,
    CONSTRAINT "PK_AppConnectionMedia" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppConnectionMedia_AppConnectionPosts_PostId" FOREIGN KEY ("PostId") REFERENCES "AppConnectionPosts" ("Id") ON DELETE CASCADE
);


CREATE TABLE "DeveloperAppMessages" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "ConversationId" uuid NOT NULL,
    "ExternalMessageId" character varying(200) NOT NULL,
    "SenderId" text,
    "ReceiverId" text,
    "Direction" integer NOT NULL,
    "MessageType" integer NOT NULL,
    "Body" character varying(5000),
    "Status" integer NOT NULL,
    "PlatformCreatedAt" timestamp with time zone,
    "ReplyToMessageId" uuid,
    "ReplyToExternalId" character varying(200),
    CONSTRAINT "PK_DeveloperAppMessages" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_DeveloperAppMessages_DeveloperAppConversations_Conversation~" FOREIGN KEY ("ConversationId") REFERENCES "DeveloperAppConversations" ("Id") ON DELETE CASCADE
);


CREATE TABLE "DeveloperAppComments" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "PostId" uuid NOT NULL,
    "ParentCommentId" uuid,
    "ExternalCommentId" character varying(200) NOT NULL,
    "AuthorId" text,
    "AuthorName" character varying(200) NOT NULL,
    "AuthorImage" text,
    "Message" character varying(5000) NOT NULL,
    "LikeCount" integer NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "IsHidden" boolean NOT NULL,
    "PlatformCreatedAt" timestamp with time zone,
    CONSTRAINT "PK_DeveloperAppComments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_DeveloperAppComments_DeveloperAppComments_ParentCommentId" FOREIGN KEY ("ParentCommentId") REFERENCES "DeveloperAppComments" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DeveloperAppComments_DeveloperAppPosts_PostId" FOREIGN KEY ("PostId") REFERENCES "DeveloperAppPosts" ("Id") ON DELETE CASCADE
);


CREATE TABLE "DeveloperAppMedia" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "PostId" uuid NOT NULL,
    "ExternalMediaId" text,
    "MediaType" integer NOT NULL,
    "Url" character varying(2000) NOT NULL,
    "Thumbnail" text,
    "Width" integer,
    "Height" integer,
    "Duration" integer,
    "DisplayOrder" integer NOT NULL,
    CONSTRAINT "PK_DeveloperAppMedia" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_DeveloperAppMedia_DeveloperAppPosts_PostId" FOREIGN KEY ("PostId") REFERENCES "DeveloperAppPosts" ("Id") ON DELETE CASCADE
);


CREATE TABLE "IntegrationMessages" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "ConversationId" uuid NOT NULL,
    "ExternalMessageId" character varying(200) NOT NULL,
    "SenderId" text,
    "ReceiverId" text,
    "Direction" integer NOT NULL,
    "MessageType" integer NOT NULL,
    "Body" character varying(5000),
    "Status" integer NOT NULL,
    "PlatformCreatedAt" timestamp with time zone,
    "ReplyToMessageId" uuid,
    "ReplyToExternalId" character varying(200),
    CONSTRAINT "PK_IntegrationMessages" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_IntegrationMessages_IntegrationConversations_ConversationId" FOREIGN KEY ("ConversationId") REFERENCES "IntegrationConversations" ("Id") ON DELETE CASCADE
);


CREATE TABLE "IntegrationComments" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "PostId" uuid NOT NULL,
    "ParentCommentId" uuid,
    "ExternalCommentId" character varying(200) NOT NULL,
    "AuthorId" text,
    "AuthorName" character varying(200) NOT NULL,
    "AuthorImage" text,
    "Message" character varying(5000) NOT NULL,
    "LikeCount" integer NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "IsHidden" boolean NOT NULL,
    "PlatformCreatedAt" timestamp with time zone,
    CONSTRAINT "PK_IntegrationComments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_IntegrationComments_IntegrationComments_ParentCommentId" FOREIGN KEY ("ParentCommentId") REFERENCES "IntegrationComments" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_IntegrationComments_IntegrationPosts_PostId" FOREIGN KEY ("PostId") REFERENCES "IntegrationPosts" ("Id") ON DELETE CASCADE
);


CREATE TABLE "IntegrationMedia" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "PostId" uuid NOT NULL,
    "ExternalMediaId" text,
    "MediaType" integer NOT NULL,
    "Url" character varying(2000) NOT NULL,
    "Thumbnail" text,
    "Width" integer,
    "Height" integer,
    "Duration" integer,
    "DisplayOrder" integer NOT NULL,
    CONSTRAINT "PK_IntegrationMedia" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_IntegrationMedia_IntegrationPosts_PostId" FOREIGN KEY ("PostId") REFERENCES "IntegrationPosts" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AppConnectionMessageAttachments" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "MessageId" uuid NOT NULL,
    "Type" integer NOT NULL,
    "Url" character varying(2000) NOT NULL,
    "Thumbnail" text,
    "Size" bigint,
    CONSTRAINT "PK_AppConnectionMessageAttachments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppConnectionMessageAttachments_AppConnectionMessages_Messa~" FOREIGN KEY ("MessageId") REFERENCES "AppConnectionMessages" ("Id") ON DELETE CASCADE
);


CREATE TABLE "DeveloperAppMessageAttachments" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "MessageId" uuid NOT NULL,
    "Type" integer NOT NULL,
    "Url" character varying(2000) NOT NULL,
    "Thumbnail" text,
    "Size" bigint,
    CONSTRAINT "PK_DeveloperAppMessageAttachments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_DeveloperAppMessageAttachments_DeveloperAppMessages_Message~" FOREIGN KEY ("MessageId") REFERENCES "DeveloperAppMessages" ("Id") ON DELETE CASCADE
);


CREATE TABLE "IntegrationMessageAttachments" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "MessageId" uuid NOT NULL,
    "Type" integer NOT NULL,
    "Url" character varying(2000) NOT NULL,
    "Thumbnail" text,
    "Size" bigint,
    CONSTRAINT "PK_IntegrationMessageAttachments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_IntegrationMessageAttachments_IntegrationMessages_MessageId" FOREIGN KEY ("MessageId") REFERENCES "IntegrationMessages" ("Id") ON DELETE CASCADE
);


CREATE UNIQUE INDEX "IX_AccessTokens_Token" ON "AccessTokens" ("Token");


CREATE UNIQUE INDEX "IX_AppConnectionComments_ExternalCommentId" ON "AppConnectionComments" ("ExternalCommentId");


CREATE INDEX "IX_AppConnectionComments_ParentCommentId" ON "AppConnectionComments" ("ParentCommentId");


CREATE INDEX "IX_AppConnectionComments_PostId" ON "AppConnectionComments" ("PostId");


CREATE INDEX "IX_AppConnectionConfigs_PlatformId" ON "AppConnectionConfigs" ("PlatformId");


CREATE UNIQUE INDEX "IX_AppConnectionConfigs_UserId_PlatformId_MenuType" ON "AppConnectionConfigs" ("UserId", "PlatformId", "MenuType");


CREATE UNIQUE INDEX "IX_AppConnectionConversations_SocialProfileId_ExternalConversa~" ON "AppConnectionConversations" ("SocialProfileId", "ExternalConversationId");


CREATE INDEX "IX_AppConnectionMedia_PostId" ON "AppConnectionMedia" ("PostId");


CREATE INDEX "IX_AppConnectionMessageAttachments_MessageId" ON "AppConnectionMessageAttachments" ("MessageId");


CREATE INDEX "IX_AppConnectionMessages_ConversationId" ON "AppConnectionMessages" ("ConversationId");


CREATE UNIQUE INDEX "IX_AppConnectionMessages_ExternalMessageId" ON "AppConnectionMessages" ("ExternalMessageId");


CREATE UNIQUE INDEX "IX_AppConnectionPlatforms_Code" ON "AppConnectionPlatforms" ("Code");


CREATE INDEX "IX_AppConnectionPosts_PlatformId" ON "AppConnectionPosts" ("PlatformId");


CREATE INDEX "IX_AppConnectionPosts_SocialProfileId" ON "AppConnectionPosts" ("SocialProfileId");


CREATE INDEX "IX_AppConnectionSocialAccounts_PlatformId" ON "AppConnectionSocialAccounts" ("PlatformId");


CREATE UNIQUE INDEX "IX_AppConnectionSocialAccounts_UserId_PlatformId" ON "AppConnectionSocialAccounts" ("UserId", "PlatformId");


CREATE UNIQUE INDEX "IX_AppConnectionSocialAuths_SocialAccountId" ON "AppConnectionSocialAuths" ("SocialAccountId");


CREATE INDEX "IX_AppConnectionSocialProfiles_ExternalProfileId" ON "AppConnectionSocialProfiles" ("ExternalProfileId");


CREATE INDEX "IX_AppConnectionSocialProfiles_SocialAccountId" ON "AppConnectionSocialProfiles" ("SocialAccountId");


CREATE INDEX "IX_AppConnectionSyncJobs_SocialAccountId" ON "AppConnectionSyncJobs" ("SocialAccountId");


CREATE INDEX "IX_AppConnectionWebhookEvents_PlatformId" ON "AppConnectionWebhookEvents" ("PlatformId");


CREATE INDEX "IX_AppConnectionWebhookEvents_Status" ON "AppConnectionWebhookEvents" ("Status");


CREATE INDEX "IX_AppConnectionWebhookLogs_PlatformId" ON "AppConnectionWebhookLogs" ("PlatformId");


CREATE INDEX "IX_AppConnectionWebhookLogs_ReceivedAt" ON "AppConnectionWebhookLogs" ("ReceivedAt");


CREATE UNIQUE INDEX "IX_DeveloperAppComments_ExternalCommentId" ON "DeveloperAppComments" ("ExternalCommentId");


CREATE INDEX "IX_DeveloperAppComments_ParentCommentId" ON "DeveloperAppComments" ("ParentCommentId");


CREATE INDEX "IX_DeveloperAppComments_PostId" ON "DeveloperAppComments" ("PostId");


CREATE INDEX "IX_DeveloperAppConfigs_PlatformId" ON "DeveloperAppConfigs" ("PlatformId");


CREATE UNIQUE INDEX "IX_DeveloperAppConfigs_UserId_PlatformId_MenuType" ON "DeveloperAppConfigs" ("UserId", "PlatformId", "MenuType");


CREATE UNIQUE INDEX "IX_DeveloperAppConversations_SocialProfileId_ExternalConversat~" ON "DeveloperAppConversations" ("SocialProfileId", "ExternalConversationId");


CREATE INDEX "IX_DeveloperAppMedia_PostId" ON "DeveloperAppMedia" ("PostId");


CREATE INDEX "IX_DeveloperAppMessageAttachments_MessageId" ON "DeveloperAppMessageAttachments" ("MessageId");


CREATE INDEX "IX_DeveloperAppMessages_ConversationId" ON "DeveloperAppMessages" ("ConversationId");


CREATE UNIQUE INDEX "IX_DeveloperAppMessages_ExternalMessageId" ON "DeveloperAppMessages" ("ExternalMessageId");


CREATE UNIQUE INDEX "IX_DeveloperAppPlatforms_Code" ON "DeveloperAppPlatforms" ("Code");


CREATE INDEX "IX_DeveloperAppPosts_PlatformId" ON "DeveloperAppPosts" ("PlatformId");


CREATE INDEX "IX_DeveloperAppPosts_SocialProfileId" ON "DeveloperAppPosts" ("SocialProfileId");


CREATE INDEX "IX_DeveloperAppSocialAccounts_PlatformId" ON "DeveloperAppSocialAccounts" ("PlatformId");


CREATE UNIQUE INDEX "IX_DeveloperAppSocialAccounts_UserId_PlatformId" ON "DeveloperAppSocialAccounts" ("UserId", "PlatformId");


CREATE UNIQUE INDEX "IX_DeveloperAppSocialAuths_SocialAccountId" ON "DeveloperAppSocialAuths" ("SocialAccountId");


CREATE INDEX "IX_DeveloperAppSocialProfiles_ExternalProfileId" ON "DeveloperAppSocialProfiles" ("ExternalProfileId");


CREATE INDEX "IX_DeveloperAppSocialProfiles_SocialAccountId" ON "DeveloperAppSocialProfiles" ("SocialAccountId");


CREATE INDEX "IX_DeveloperAppSyncJobs_SocialAccountId" ON "DeveloperAppSyncJobs" ("SocialAccountId");


CREATE INDEX "IX_DeveloperAppWebhookEvents_PlatformId" ON "DeveloperAppWebhookEvents" ("PlatformId");


CREATE INDEX "IX_DeveloperAppWebhookEvents_Status" ON "DeveloperAppWebhookEvents" ("Status");


CREATE INDEX "IX_DeveloperAppWebhookLogs_PlatformId" ON "DeveloperAppWebhookLogs" ("PlatformId");


CREATE INDEX "IX_DeveloperAppWebhookLogs_ReceivedAt" ON "DeveloperAppWebhookLogs" ("ReceivedAt");


CREATE INDEX "IX_IntegrationAppConfigs_PlatformId" ON "IntegrationAppConfigs" ("PlatformId");


CREATE UNIQUE INDEX "IX_IntegrationAppConfigs_UserId_PlatformId_MenuType" ON "IntegrationAppConfigs" ("UserId", "PlatformId", "MenuType");


CREATE UNIQUE INDEX "IX_IntegrationComments_ExternalCommentId" ON "IntegrationComments" ("ExternalCommentId");


CREATE INDEX "IX_IntegrationComments_ParentCommentId" ON "IntegrationComments" ("ParentCommentId");


CREATE INDEX "IX_IntegrationComments_PostId" ON "IntegrationComments" ("PostId");


CREATE UNIQUE INDEX "IX_IntegrationConversations_SocialProfileId_ExternalConversati~" ON "IntegrationConversations" ("SocialProfileId", "ExternalConversationId");


CREATE INDEX "IX_IntegrationMedia_PostId" ON "IntegrationMedia" ("PostId");


CREATE INDEX "IX_IntegrationMessageAttachments_MessageId" ON "IntegrationMessageAttachments" ("MessageId");


CREATE INDEX "IX_IntegrationMessages_ConversationId" ON "IntegrationMessages" ("ConversationId");


CREATE UNIQUE INDEX "IX_IntegrationMessages_ExternalMessageId" ON "IntegrationMessages" ("ExternalMessageId");


CREATE UNIQUE INDEX "IX_IntegrationPlatforms_Code" ON "IntegrationPlatforms" ("Code");


CREATE INDEX "IX_IntegrationPosts_PlatformId" ON "IntegrationPosts" ("PlatformId");


CREATE INDEX "IX_IntegrationPosts_SocialProfileId" ON "IntegrationPosts" ("SocialProfileId");


CREATE INDEX "IX_IntegrationSocialAccounts_PlatformId" ON "IntegrationSocialAccounts" ("PlatformId");


CREATE UNIQUE INDEX "IX_IntegrationSocialAccounts_UserId_PlatformId" ON "IntegrationSocialAccounts" ("UserId", "PlatformId");


CREATE UNIQUE INDEX "IX_IntegrationSocialAuths_SocialAccountId" ON "IntegrationSocialAuths" ("SocialAccountId");


CREATE INDEX "IX_IntegrationSocialProfiles_ExternalProfileId" ON "IntegrationSocialProfiles" ("ExternalProfileId");


CREATE INDEX "IX_IntegrationSocialProfiles_SocialAccountId" ON "IntegrationSocialProfiles" ("SocialAccountId");


CREATE INDEX "IX_IntegrationSyncJobs_SocialAccountId" ON "IntegrationSyncJobs" ("SocialAccountId");


CREATE INDEX "IX_IntegrationWebhookEvents_PlatformId" ON "IntegrationWebhookEvents" ("PlatformId");


CREATE INDEX "IX_IntegrationWebhookEvents_Status" ON "IntegrationWebhookEvents" ("Status");


CREATE INDEX "IX_IntegrationWebhookLogs_PlatformId" ON "IntegrationWebhookLogs" ("PlatformId");


CREATE INDEX "IX_IntegrationWebhookLogs_ReceivedAt" ON "IntegrationWebhookLogs" ("ReceivedAt");


CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");


