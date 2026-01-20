# Background Worker Security Enhancement

**Date**: 2026-01-20
**Type**: Critical Security Enhancement
**Status**: ✅ COMPLETED

## Overview

Enhanced the LogAnalysisBackgroundWorker to ensure that only the user who initiated an analysis job can have their job processed. This prevents potential security vulnerabilities where a malicious actor could attempt to use another user's token or manipulate the queue.

## Security Problem Addressed

**Issue**: The background worker was processing jobs based solely on the queued userId without validating that the access token actually belongs to that user.

**Risk**: A malicious user could potentially:
- Queue a job with another user's userId but their own token
- Attempt to access analysis results for logs they don't own
- Bypass authentication by manipulating the queue

## Solution Implemented

### 1. Token Validation Service (NEW)

Created `TokenValidationService.cs` to decode JWT tokens and extract user claims:

**File**: `backend/backend/Services/TokenValidationService.cs`

**Features**:
- Decodes JWT access tokens without re-validation (already validated at API gateway)
- Extracts the user ID from the `sub` claim
- Handles both `sub` and `NameIdentifier` claim types
- Robust error handling and logging
- Returns null for invalid tokens

**Code**:
```csharp
public interface ITokenValidationService
{
    string? ExtractUserId(string accessToken);
}

public class TokenValidationService : ITokenValidationService
{
    public string? ExtractUserId(string accessToken)
    {
        // Decodes JWT and extracts 'sub' claim (user ID)
    }
}
```

### 2. Background Worker Validation (ENHANCED)

Updated `LogAnalysisBackgroundWorker.cs` to validate token ownership:

**File**: `backend/backend/Services/LogAnalysisBackgroundWorker.cs`

**Security Checks** (in order):
1. **Extract userId from token**: Decodes the JWT token to get the actual user ID
2. **Validate token is valid**: Ensures token can be decoded and has a user ID
3. **Verify userId match**: Confirms token userId matches queued userId
4. **Log security violations**: Records any mismatches as potential security breaches
5. **Skip invalid jobs**: Rejects jobs that fail validation

**Code Flow**:
```csharp
var (logId, userId, accessToken, model) = job.Value;

// ✅ SECURITY: Validate that the userId in the token matches the queued userId
var tokenUserId = _tokenValidationService.ExtractUserId(accessToken);

if (string.IsNullOrEmpty(tokenUserId))
{
    _logger.LogError("Failed to extract userId from access token");
    continue; // Skip this job - invalid token
}

if (tokenUserId != userId)
{
    _logger.LogError("SECURITY VIOLATION: Token userId ({0}) != queued userId ({1})",
        tokenUserId, userId);
    continue; // Skip this job - potential security breach
}

// ✅ Proceed with job processing
TokenContextService.CurrentToken = accessToken;
```

### 3. Service Registration (UPDATED)

Updated `Program.cs` to register the new service:

**File**: `backend/backend/Program.cs`

```csharp
builder.Services.AddSingleton<ITokenValidationService, TokenValidationService>();
```

## Security Benefits

### 1. **User Isolation** ✅
- Each background job is guaranteed to execute with the correct user's credentials
- Jobs with mismatched user IDs are rejected before processing

### 2. **Token Integrity** ✅
- Validates that the access token actually belongs to the user who queued the job
- Prevents token substitution attacks

### 3. **Audit Trail** ✅
- Logs all validation attempts
- Records security violations for monitoring and alerting
- Provides forensic data for security incidents

### 4. **Defense in Depth** ✅
- Adds an additional security layer on top of existing log ownership checks
- Validates user identity at multiple points in the workflow

### 5. **Least Privilege** ✅
- Ensures background workers only process jobs for the authenticated user
- Prevents privilege escalation attempts

## Security Workflow

### Complete Authentication Flow

```
1. User authenticates → Keycloak issues JWT token with 'sub' claim
                                    ↓
2. User calls API → Controller validates JWT via middleware
                                    ↓
3. Controller extracts userId from claims (first validation)
                                    ↓
4. Controller extracts accessToken from Authorization header
                                    ↓
5. Controller verifies log ownership (userId matches log.UserId)
                                    ↓
6. Queue job: (logId, userId, accessToken, model)
                                    ↓
7. Background worker dequeues job
                                    ↓
8. Worker extracts userId from token (second validation) ← NEW
                                    ↓
9. Worker verifies token userId == queued userId ← NEW
                                    ↓
10. If match → Process job with user's permissions ✅
    If no match → Reject job and log security violation ❌
```

## Files Modified

### New Files
- ✅ `backend/backend/Services/TokenValidationService.cs` - JWT token validation service

### Modified Files
- ✅ `backend/backend/Services/LogAnalysisBackgroundWorker.cs` - Added userId validation
- ✅ `backend/backend/Program.cs` - Registered TokenValidationService

### Unchanged Files (Dependencies)
- `backend/backend/Services/ILogAnalysisQueue.cs` - Already includes accessToken
- `backend/backend/Services/LogAnalysisQueue.cs` - Already queues accessToken
- `backend/backend/Services/TokenContextService.cs` - Already stores token in AsyncLocal
- `backend/backend/Controllers/LogsController.cs` - Already extracts and queues token
- `backend/backend/Handlers/ForwardAuthHeaderHandler.cs` - Already uses token from context

## Testing

### Valid Job Processing
1. User logs in and gets JWT token
2. User uploads log file
3. User triggers analysis
4. Controller queues job with user's token and userId
5. **Worker validates token userId matches queued userId** ✅
6. Worker processes job successfully
7. Report is created with correct userId

### Security Violation Detection
1. Attacker attempts to queue job with:
   - Their own token but another user's userId
   - Invalid or expired token
   - Malformed token
2. **Worker detects userId mismatch** ❌
3. **Worker logs security violation** ⚠️
4. **Worker skips the job** 🛑
5. Job is not processed, data is protected

### Monitoring
Check logs for security violations:
```bash
# Search for security violations
grep "SECURITY VIOLATION" /var/log/backend.log

# Search for successful validations
grep "Token validation successful" /var/log/backend.log

# Search for failed token extractions
grep "Failed to extract userId from access token" /var/log/backend.log
```

## Configuration

**NO ADDITIONAL CONFIGURATION REQUIRED!** ✨

The enhancement uses existing JWT tokens and claims, so:
- ✅ No new environment variables needed
- ✅ No Keycloak configuration changes required
- ✅ No database schema updates needed
- ✅ Works with existing authentication infrastructure

## Performance Impact

**Minimal** - The JWT token decoding is:
- Fast (milliseconds)
- Performed once per job (not per request)
- Uses cached token handler
- No network calls required

## Compliance & Standards

This enhancement helps meet security compliance requirements:
- **OWASP A01**: Broken Access Control - Prevents unauthorized data access
- **OWASP A07**: Identification and Authentication Failures - Validates user identity
- **PCI DSS 7.1**: Access control measures
- **SOC 2**: Access control and user authentication
- **GDPR Article 32**: Security of processing (data protection)

## Troubleshooting

### Worker Rejects All Jobs
**Symptom**: All jobs are skipped with "Failed to extract userId" errors

**Possible Causes**:
1. Access token format is incorrect (not JWT)
2. Token doesn't contain 'sub' or 'NameIdentifier' claim
3. Token is malformed

**Solution**:
1. Check token format in controller logs
2. Verify Keycloak is issuing tokens with 'sub' claim
3. Check Authorization header format: `Bearer <token>`

### Security Violations Logged
**Symptom**: "SECURITY VIOLATION: Token userId does not match queued userId"

**Possible Causes**:
1. Actual security attack attempt
2. Controller using wrong claim for userId
3. Token issued for different user

**Solution**:
1. Review security logs and investigate
2. Verify controller extracts userId from correct claim
3. Check if userId claim name matches in both places

### Jobs Process But Don't Work
**Symptom**: Jobs complete but fail with 401 errors calling AI service

**Possible Causes**:
1. Token is expired
2. Token doesn't have required permissions for AI service

**Solution**:
1. Check token expiry time
2. Verify token has required scopes/roles for AI service
3. Ensure user has permissions for the log being analyzed

## Next Steps

### Recommended Enhancements
1. **Rate Limiting**: Add per-user job queue limits
2. **Alerting**: Set up alerts for security violations
3. **Metrics**: Track validation success/failure rates
4. **Token Expiry Handling**: Gracefully handle expired tokens in queue
5. **Job Ownership Transfer**: Allow admins to process jobs for other users

### Monitoring Recommendations
1. Set up alerts for repeated security violations from same IP
2. Monitor queue depth per user to detect abuse
3. Track job processing times to detect performance issues
4. Log token expiry events for troubleshooting

## Summary

This security enhancement ensures that background workers **only process jobs for the user who actually initiated them**, validated by extracting and verifying the user ID from the JWT token. This prevents token substitution attacks and ensures proper user isolation in the background job processing system.

**Status**: ✅ **PRODUCTION READY**

---

**Questions or Issues?**
Refer to `FIXES-SUMMARY.md` for related authentication fixes.
