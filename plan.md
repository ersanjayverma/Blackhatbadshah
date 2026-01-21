# Blackhatbadshah - Bug Fixes & Feature Implementation Plan

**Created:** 2026-01-21
**Status:** In Progress

---

## Task Checklist

### P0 BUG FIX #1: Worker selection makes ReportListItem blank
- [ ] Identify root cause of ReportListItem becoming blank on worker selection
- [ ] Fix state handling in frontend
- [ ] Ensure reports remain visible after worker selection
- [ ] Build passes

### P0 BUG FIX #2: bhbworker keeps restarting + systemd mount namespace error
- [ ] Provide diagnostic commands
- [ ] Fix systemd service configuration
- [ ] Create missing /opt/bhbworker directory
- [ ] Add RestartSec and monitoring recommendations
- [ ] Verify worker runs consistently

### P0 UX FIX #3: UI text color/background inconsistent and hidden text
- [ ] Identify conflicting CSS in app.css vs site.css
- [ ] Fix "Logs by Source" tag style (white background hiding text)
- [ ] Ensure consistent theme contract for tags/badges/chips
- [ ] Fix layout overlaps where text is hidden
- [ ] Build passes

### P0 DESIGN FIX #4: Worker must be per-user with unique PSK
- [ ] Add UserWorkerConfig entity to database
- [ ] Create EF migration
- [ ] Add backend endpoints (GET/POST worker-config)
- [ ] Implement PSK generation and rotation
- [ ] Update frontend Worker dashboard page
- [ ] Implement worker authentication with WorkerId + PSK
- [ ] Build passes

### P0 BUG FIX #5: Process not being killed from frontend
- [ ] Identify existing cancel/kill mechanism
- [ ] Add Job entity with cancellation support
- [ ] Add POST /api/jobs/{id}/cancel endpoint
- [ ] Implement worker cancellation token checking
- [ ] Update UI to show cancellation progress
- [ ] Build passes

### P1 FEATURE: Add Profile photo upload under "My Profile"
- [ ] Rename "My Plan" to "My Profile" in NavMenu
- [ ] Add photo upload UI to Subscription.razor
- [ ] Add backend endpoints (POST /api/profile/photo, GET /api/profile)
- [ ] Integrate with blob storage
- [ ] Display photo in profile and header
- [ ] Build passes

---

## Progress Log

### 2026-01-21 - Session Start
- Explored codebase structure
- Identified key files for each fix
- Created plan.md

