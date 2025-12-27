# Security Fix Changelog

**Date**: 2025-12-26
**Type**: Critical Security Vulnerability Remediation

## Summary

All hardcoded credentials and secrets have been removed from the codebase and replaced with environment variable management. This prevents sensitive data from being exposed in version control.

## Critical Vulnerabilities Fixed

### 1. Exposed Anthropic API Key
- **File**: `infra/dockerCompose/docker-compose.yml`
- **Line**: 11
- **Issue**: Anthropic API key hardcoded in plain text
- **Resolution**: Replaced with environment variable `${ANTHROPIC_API_KEY}`
- **Action Required**: Rotate the exposed API key immediately

### 2. Hardcoded AWS Credentials
- **File**: `Dockerfile`
- **Lines**: 15-16
- **Issue**: AWS Access Key ID and Secret Access Key hardcoded
- **Resolution**: Replaced with environment variables
- **Action Required**: Rotate AWS credentials in IAM Console

### 3. Exposed Azure SQL Database Password
- **File**: `backend/backend/appsettings.json`
- **Line**: 10
- **Issue**: Database connection string with password in plain text
- **Resolution**: Removed connection string, now loaded from environment
- **Action Required**: Change database password in Azure Portal

### 4. Exposed Azure Blob Storage Key
- **File**: `backend/backend/appsettings.json`
- **Line**: 13
- **Issue**: Storage account key hardcoded
- **Resolution**: Removed storage key, now loaded from environment
- **Action Required**: Regenerate storage account key in Azure Portal

## Files Modified

### Configuration Files
- ✅ `.gitignore` - Enhanced to exclude all .env files and Python venv
- ✅ `Dockerfile` - Removed hardcoded AWS credentials
- ✅ `docker-compose.yml` - Replaced all secrets with env vars
- ✅ `appsettings.json` - Removed database and storage credentials
- ✅ `README.md` - Added comprehensive setup documentation

### New Files Created
- ✅ `.env.example` - Template for environment variables (root)
- ✅ `infra/dockerCompose/.env.example` - Template for Docker Compose
- ✅ `.dockerignore` - Prevents secrets in Docker builds (root)
- ✅ `ai/.dockerignore` - Prevents secrets in AI service builds
- ✅ `SECURITY.md` - Security guidelines and documentation
- ✅ `setup.sh` - Automated interactive setup script
- ✅ `CHANGELOG_SECURITY_FIX.md` - This file

## Environment Variables Required

The application now requires the following environment variables:

### Anthropic (AI Service)
- `ANTHROPIC_API_KEY`

### AWS (Backend Service)
- `AWS_ACCESS_KEY_ID`
- `AWS_SECRET_ACCESS_KEY`
- `AWS_REGION`
- `AWS_DEFAULT_REGION`

### Azure SQL Database (Backend Service)
- `ConnectionStrings__DefaultConnection`

### Azure Blob Storage (Backend Service)
- `AzureBlob__ConnectionString`

## Setup Instructions

### Option 1: Automated (Recommended)
```bash
./setup.sh
```

### Option 2: Manual
```bash
cp infra/dockerCompose/.env.example infra/dockerCompose/.env
# Edit .env with your credentials
cd infra/dockerCompose
docker-compose up -d
```

## Security Improvements

1. **Environment Variable Management**: All secrets now managed via .env files
2. **Git Ignore Enhancement**: Comprehensive patterns to prevent accidental commits
3. **Docker Build Security**: .dockerignore files prevent secrets in images
4. **Documentation**: Complete security guidelines and setup instructions
5. **Automated Setup**: Interactive script for secure configuration

## Immediate Action Items

⚠️ **CRITICAL - Do These Now**:

1. **Rotate Anthropic API Key**
   - Go to: https://console.anthropic.com
   - Generate new API key
   - Revoke old key

2. **Rotate AWS Credentials**
   - Go to: AWS IAM Console
   - Deactivate old access key
   - Create new access key

3. **Change Azure SQL Password**
   - Go to: Azure Portal → SQL Databases
   - Reset database user password

4. **Regenerate Azure Storage Key**
   - Go to: Azure Portal → Storage Accounts
   - Regenerate account key

5. **Set Up Environment**
   - Run `./setup.sh` with new credentials
   - Test application startup

6. **Audit Access Logs**
   - Check for unauthorized usage of exposed credentials
   - Review CloudTrail (AWS), Azure Activity Logs

## Verification Checklist

- [ ] All exposed credentials have been rotated
- [ ] `.env` file created with new credentials
- [ ] Application starts successfully with new config
- [ ] No secrets remain in tracked files
- [ ] `.gitignore` properly configured
- [ ] Team members notified of security update
- [ ] Access logs reviewed for suspicious activity
- [ ] Development environments updated

## Best Practices Going Forward

1. Never commit credentials to version control
2. Use different credentials for dev/staging/prod
3. Rotate credentials regularly (quarterly minimum)
4. Use secrets management services (Azure Key Vault, AWS Secrets Manager)
5. Enable audit logging on all services
6. Review code for secrets before committing
7. Use pre-commit hooks to scan for secrets

## Questions or Issues?

Refer to `SECURITY.md` for detailed security guidelines.
