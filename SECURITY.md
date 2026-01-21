# Security Policy & Credential Management

## Overview
This document describes how Blackhatbadshah manages credentials, secrets, and sensitive data for all services and workers.

## Credentials & Secrets

All credentials and secrets are managed via environment variables and configuration files. The following are required:

- **Anthropic API Key** (`ANTHROPIC_API_KEY`)
- **AWS Credentials** (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_REGION`)
- **Azure SQL Connection String** (`ConnectionStrings__DefaultConnection`)
- **Azure Blob Storage Connection String** (`AzureBlob__ConnectionString`)
- **Razorpay API Keys** (in `appsettings.json`)
- **Keycloak Client Secrets** (in `appsettings.json`)

## Setup Instructions

1. Copy `.env.example` to `.env` and fill in your actual credentials:
   ```bash
   cp .env.example .env
   ```
2. Edit `.env` and replace all placeholder values with your actual credentials.
3. Never commit `.env` to version control. `.gitignore` excludes `.env`, `.env.*`, `.env.local`.
4. For worker agents, API keys and worker IDs are generated in the dashboard and stored in `appsettings.json` on the worker host.

### .gitignore

`.env` and all sensitive config files are excluded from version control. Only `.env.example` is tracked.

### Running Services

All services (backend, frontend, AI, worker) read credentials from environment variables or config files. Docker Compose loads from `.env` automatically.

## Security Best Practices

1. **Never commit secrets**: `.env` and `appsettings.json` with secrets must not be tracked.
2. **Rotate exposed credentials immediately**: If any credential is exposed, rotate it and update all services.
3. **Use separate credentials for dev, staging, prod**: Never reuse secrets across environments.
4. **Limit permissions**: Grant only the minimum required permissions for AWS, Azure, and API keys.
5. **Worker API keys**: Each worker agent uses a unique API key and Worker ID. Keys are only shown once in the dashboard.

## Credential Rotation

If any credential is compromised:
1. Revoke/rotate the credential immediately
2. Update `.env` or `appsettings.json` with the new value
3. Restart all services
4. Audit logs for unauthorized access

## Additional Recommendations

- Use a secrets manager (Azure Key Vault, AWS Secrets Manager) for production
- Enable HTTPS for all services
- Monitor access logs and audit regularly
- Use strong, randomly generated passwords and API keys
   - HashiCorp Vault

2. Enable audit logging for all services

3. Implement IP whitelisting where possible

4. Use managed identities when running on cloud platforms

5. Regularly review and rotate credentials

## Questions?

For security concerns or questions, contact your security team immediately.
