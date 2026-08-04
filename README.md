# Social Media Integration Platform

Professional full-stack app for managing social media integrations (Facebook, Instagram, WhatsApp, YouTube, LinkedIn, TikTok, and more).

## Stack

- **Backend:** ASP.NET Core 8 — Clean Architecture, Repository Pattern, Fluent API
- **Frontend:** Angular 19 + Angular Material
- **Auth:** JWT public login + invite signup with access token
- **Integrations (live):** Facebook, Instagram, WhatsApp (Meta Graph API + Webhooks)

## Solution Structure

```
backend/
  SocialMedia.Domain/          # Entities, enums, repository interfaces
  SocialMedia.Application/     # DTOs, service interfaces, business services
  SocialMedia.Infrastructure/  # EF Core, repositories, Meta API clients, JWT
  SocialMedia.Api/             # Controllers, Program.cs

frontend/                      # Angular Material SPA
```

## Features

- Public login / in-app invite signup (access-token gated)
- Integration hub with Connect cards per platform
- Platform-styled create-post UIs (Facebook, Instagram, TikTok, …)
- Inbox with Comments / Messages tabs (WhatsApp = Messages only)
- Separate Meta OAuth endpoints for Facebook, Instagram, WhatsApp
- Webhook verification + processing for all three Meta platforms
- Dashboard, Analytics, Connected Accounts, Settings

## Quick Start

### Backend
```bash
cd backend
dotnet run --project SocialMedia.Api
```
Swagger: http://localhost:5080/swagger

### Frontend
```bash
cd frontend
npm install
npm start
```
App: http://localhost:4200

### Default credentials
- Admin: `Admin@gmail.com` / `Admin@321`
- Invite token: `INVITE-SOCIALHUB-2026`

Configure Meta App credentials:
- Frontend auth URLs: `frontend/src/environments/environment.ts` → `meta`
- Backend webhooks / Graph: `backend/SocialMedia.Api/appsettings.json` → `MetaSettings`

SQL Server: `DefaultConnection` → `SocialMediaHubDb` on `DESKTOP-6L1G3DP`.

### Webhook endpoints (backend only)
- `GET  /api/Webhooks/Connection?platformCode=facebook&hub.mode=...`
- `POST /api/Webhooks/Subscribe?platformCode=facebook`
- `POST /api/Webhooks/Received?platformCode=facebook`

### Connect flow
1. User clicks **Connect** → frontend opens Meta Login popup
2. Meta redirects to `/integrations/callback/{facebook|instagram|whatsapp}?code=...`
3. Callback page POSTs the code to `Integrations/FacebookCallback` (or Instagram / WhatsApp)
4. Backend exchanges code (with App Secret), stores SocialAccount → SocialAuth → SocialProfiles → SyncJob

Account listing / disconnect live on `SocialAccounts` (`GetPlatformCards`, `GetConnectedAccounts`, `Disconnect`).
