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
3. Add **one** OAuth redirect URI (Facebook Login → Valid OAuth Redirect URIs) — the backend Callback:
   - Local: `http://localhost:5080/api/Integrations/Callback`
   - Production: `https://<your-backend>/api/Integrations/Callback`
4. Point **all** product webhooks to one Callback URL:
   - `GET/POST https://<your-backend>/api/webhooks`
5. Use the same verify token in Meta for each product, or any of the tokens in `MetaSettings.*WebhookVerifyToken` (the shared endpoint accepts all of them).

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
