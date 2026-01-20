# Implementation Plan - Blackhatbadshah Multi-Service Enhancement

**Status**: IN PROGRESS
**Started**: 2026-01-20

---

## A) FIX UI COLOR CONTRAST + INVISIBLE TEXT

### A.1 Consolidate Theme Variables in app.css ❌
- [ ] Define complete CSS variable set in frontend/frontend/wwwroot/css/app.css
- [ ] Variables: --bg, --surface, --surface-2, --border, --text, --text-muted, --link, --accent, --accent-hover, --danger, --success, --warning, --input-bg, --input-border, --focus-ring
- [ ] Ensure body, cards, tables, modals, toasts, forms inherit from variables

### A.2 Eliminate site.css Conflicts ❌
- [ ] Remove hardcoded colors from frontend/frontend/wwwroot/css/site.css
- [ ] Replace all fixed colors with CSS variables
- [ ] Ensure Bootstrap defaults don't override theme

### A.3 Fix Component Readability ❌
- [ ] Navbar + sidebar nav links
- [ ] Buttons (primary/secondary/outline)
- [ ] Forms (input placeholder, labels, disabled fields)
- [ ] Tables (thead/tbody text)
- [ ] Alerts/toasts
- [ ] Chat window component

### A.4 Implement Light/Dark Toggle ❌
- [ ] Add theme toggle switch in MainLayout/NavMenu
- [ ] Toggle applies data-theme attribute on body
- [ ] Light theme variable set
- [ ] Persist selection in localStorage
- [ ] JavaScript/Blazor interop for theme switching

---

## B) WORKER REGISTRATION - SERVICE IDENTITY (NOT USER-BASED)

### B.1 Add WorkerAgent Data Model ❌
- [ ] Create backend/backend/Data/Entities/WorkerAgent.cs
- [ ] Table: WorkerAgents (Id, WorkspaceId, Name, ApiKeyHash, Status, CreatedByUserId, CreatedAt, RevokedAt, LastSeenAt)
- [ ] Add DbSet to AppDbContext
- [ ] Create EF migration

### B.2 Add Worker Management Endpoints ❌
- [ ] POST /api/workers/register (JWT auth, Owner/Admin only)
- [ ] GET /api/workspaces/{workspaceId}/workers
- [ ] POST /api/workers/{workerId}/revoke
- [ ] POST /api/workers/{workerId}/rotate-key
- [ ] Implement PBKDF2 hash for API keys
- [ ] Return apiKey ONCE on create/rotate

### B.3 Worker Authentication Scheme ❌
- [ ] Add WorkerKeyAuthenticationHandler in backend/backend/Handlers/
- [ ] Validate X-Worker-Key header
- [ ] Build ClaimsPrincipal (sub=worker:{id}, role=Worker, workspaceId)
- [ ] Register scheme in Program.cs

### B.4 Worker-Only Endpoints ❌
- [ ] POST /api/worker/jobs/{jobId}/progress (WorkerKey auth only)
- [ ] POST /api/worker/jobs/{jobId}/complete (WorkerKey auth only)
- [ ] POST /api/worker/jobs/{jobId}/fail (WorkerKey auth only)
- [ ] Workspace-scoped access validation

### B.5 Remove User Token Dependency ❌
- [ ] Update background worker to use WorkerKey for API calls
- [ ] Remove reliance on user JWT in worker pipeline

---

## C) WORKER MANAGEMENT UI - ADMIN ONLY

### C.1 Add Workers Admin Page ❌
- [ ] Create frontend/frontend/Pages/Admin/Workers.razor
- [ ] Route: /admin/workers or /workspaces/{id}/workers
- [ ] Worker list table (name, status, createdAt, lastSeenAt)
- [ ] Register Worker button/form
- [ ] Revoke/Rotate key buttons

### C.2 API Key Display Modal ❌
- [ ] Show apiKey ONCE in modal with copy button
- [ ] Clear apiKey after modal close
- [ ] Security warning: "This key will only be shown once"

### C.3 Enforce Role-Based Visibility ❌
- [ ] Nav link visible only to Owner/Admin role
- [ ] Check Keycloak role claims
- [ ] Frontend route guard
- [ ] Backend endpoint authorization

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

**Completed**: 0/23 tasks
**In Progress**: 0
**Pending**: 23

**Current Task**: A.1 - Consolidate Theme Variables in app.css
