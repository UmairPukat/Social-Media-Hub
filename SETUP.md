# Social Media Integration — Developer Guide

## Architecture

```
backend/
  SocialMedia.Domain          Entities, enums, repository interfaces
  SocialMedia.Application     DTOs, service interfaces, business services
  SocialMedia.Infrastructure  EF Core + Fluent API, repositories, Meta Graph client
  SocialMedia.API             Thin controllers, JWT, Swagger, DI wiring
```

## Demo credentials

After first API start:

- Login: `Admin@gmail.com` / `Admin@321`
- Signup access token (Invite Signup menu): `SMI-DEMO-ACCESS-2026`

## Run backend

```bash
cd backend/SocialMedia.Api
dotnet restore
dotnet run
```

API + Swagger: http://localhost:5080/swagger

## Run frontend

```bash
cd frontend/social-media-app
npm install
npm start
```

App: http://localhost:4200

## Meta setup

1. Create a Meta App in developers.facebook.com
2. Put App Id / Secret into `backend/SocialMedia.Api/appsettings.json` → `MetaSettings`
3. Add OAuth redirect URIs:
   - http://localhost:4200/oauth/facebook/callback
   - http://localhost:4200/oauth/instagram/callback
   - http://localhost:4200/oauth/whatsapp/callback
4. Point webhooks to your public HTTPS URL:
   - GET/POST `/api/webhooks/facebook`
   - GET/POST `/api/webhooks/instagram`
   - GET/POST `/api/webhooks/whatsapp`
5. Match verify tokens with `MetaSettings.*WebhookVerifyToken`

## Implemented platform capabilities

| Feature | Facebook | Instagram | WhatsApp |
|---------|----------|-----------|----------|
| Separate Meta OAuth | Yes | Yes | Yes |
| Webhooks | Yes | Yes | Yes |
| Create / Get / Delete posts | Yes | Yes | N/A |
| Comments get / post / hide / delete | Yes | Yes | N/A |
| Messages get / post / delete | Yes | Yes | Yes |

## Angular menus

Dashboard, Integrations, Create Post, Posts, Inbox, Connected Accounts, Analytics, Invite Signup, Settings
