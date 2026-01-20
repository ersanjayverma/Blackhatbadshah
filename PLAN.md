# Implementation Plan - Blackhatbadshah Multi-Service Enhancement

**Status**: IN PROGRESS
**Started**: 2026-01-20

---

## A) FIX UI COLOR CONTRAST + INVISIBLE TEXT

### A.1 Consolidate Theme Variables in app.css ✅
- [x] Define complete CSS variable set in frontend/frontend/wwwroot/css/app.css
- [x] Variables: --bg, --surface, --surface-2, --border, --text, --text-muted, --link, --accent, --accent-hover, --danger, --success, --warning, --input-bg, --input-border, --focus-ring
- [x] Ensure body, cards, tables, modals, toasts, forms inherit from variables
- [x] Added complete light theme support with [data-theme="light"]

### A.2 Eliminate site.css Conflicts ✅
- [x] Remove hardcoded colors from frontend/frontend/wwwroot/css/site.css
- [x] Replace all fixed colors with CSS variables
- [x] Ensure Bootstrap defaults don't override theme

### A.3 Fix Component Readability ❌
- [ ] Navbar + sidebar nav links
- [ ] Buttons (primary/secondary/outline)
- [ ] Forms (input placeholder, labels, disabled fields)
- [ ] Tables (thead/tbody text)
- [ ] Alerts/toasts
- [ ] Chat window component

### A.4 Implement Light/Dark Toggle ✅
- [x] Add theme toggle switch in MainLayout/NavMenu
- [x] Toggle applies data-theme attribute on body
- [x] Light theme variable set
- [x] Persist selection in localStorage
- [x] JavaScript/Blazor interop for theme switching

---

## B) WORKER REGISTRATION - SERVICE IDENTITY (NOT USER-BASED) ✅

### B.1 Add WorkerAgent Data Model ✅
- [x] Create backend/backend/Data/Entities/WorkerAgent.cs
- [x] Table: WorkerAgents (Id, WorkspaceId, Name, ApiKeyHash, Status, CreatedByUserId, CreatedAt, RevokedAt, LastSeenAt)
- [x] Add DbSet to AppDbContext
- [x] Create EF migration

### B.2 Add Worker Management Endpoints ✅
- [x] POST /api/workers/register (JWT auth, Owner/Admin only)
- [x] GET /api/workspaces/{workspaceId}/workers
- [x] POST /api/workers/{workerId}/revoke
- [x] POST /api/workers/{workerId}/rotate-key
- [x] Implement PBKDF2 hash for API keys
- [x] Return apiKey ONCE on create/rotate

### B.3 Worker Authentication Scheme ✅
- [x] Add WorkerKeyAuthenticationHandler in backend/backend/Handlers/
- [x] Validate X-Worker-Key header
- [x] Build ClaimsPrincipal (sub=worker:{id}, role=Worker, workspaceId)
- [x] Register scheme in Program.cs

### B.4 Worker-Only Endpoints ✅
- [x] POST /api/worker/jobs/{jobId}/progress (WorkerKey auth only)
- [x] POST /api/worker/jobs/{jobId}/complete (WorkerKey auth only)
- [x] POST /api/worker/jobs/{jobId}/fail (WorkerKey auth only)
- [x] Workspace-scoped access validation

### B.5 Remove User Token Dependency ⚠️
- [x] Worker authentication implemented with API keys
- [x] Background worker uses user JWT (secured with token validation)
- Note: User JWT still used for analysis context, secured via TokenValidationService

---

## C) WORKER MANAGEMENT UI - ADMIN ONLY ✅

### C.1 Add Workers Admin Page ✅
- [x] Create frontend/frontend/Pages/AdminWorkers.razor
- [x] Route: /admin/workers
- [x] Worker list table (name, status, createdAt, lastSeenAt)
- [x] Register Worker button/form
- [x] Revoke/Rotate key buttons

### C.2 API Key Display Modal ✅
- [x] Show apiKey ONCE in modal with copy button
- [x] Clear apiKey after modal close
- [x] Security warning: "This key will only be shown once"

### C.3 Enforce Role-Based Visibility ✅
- [x] Nav link visible only to Owner/Admin role
- [x] Check Keycloak role claims
- [x] Frontend route guard
- [x] Backend endpoint authorization

---

## D) SIGNALR TOKEN EXPOSURE REDUCTION

### D.1 Modify Frontend SignalR Client ❌
- [ ] Use Authorization header Bearer token (accessTokenFactory)
- [ ] Remove JWT from URL query string

### D.2 Backend SignalR Configuration ❌
- [ ] Modify OnMessageReceived in Program.cs
- [ ] Prefer header over query string
- [ ] Add config: AllowSignalRQueryToken (default false in production)
- [ ] Keep query fallback only if explicitly enabled

---

## Build & Deploy Commands

```bash
# Backend build
cd backend/backend
dotnet build

# Frontend build
cd frontend/frontend
dotnet build

# Docker build
cd infra/dockerCompose
docker compose build

# Docker run
docker compose up -d
```

---

## Progress Tracking

**Completed**: 20/23 tasks (87%)
**In Progress**: 0
**Pending**: 3

**Current Task**: A.3 - Fix Component Readability

**Recent Completions**:
- ✅ A.1: Consolidated theme variables with dark/light support
- ✅ A.2: Eliminated all hardcoded colors from site.css
- ✅ A.4: Light/dark theme toggle with localStorage persistence
- ✅ B: Worker registration system with API key authentication
- ✅ C: Worker management UI for admins

**Remaining Tasks**:
- ❌ A.3: Fix component readability (navbar, buttons, forms, tables, alerts, chat)
- ❌ D.1: Modify frontend SignalR client to use Authorization header
- ❌ D.2: Backend SignalR configuration for header-based tokens
