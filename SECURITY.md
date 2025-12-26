# Security Configuration Guide

## Overview
This document outlines the security configuration for the Blackhatbadshah application and provides instructions for setting up credentials properly.

## Critical Security Changes

All hardcoded credentials and secrets have been removed from the codebase and replaced with environment variables. This includes:

1. **Anthropic API Key** - Previously exposed in `docker-compose.yml`
2. **AWS Credentials** - Previously hardcoded in `Dockerfile`
3. **Azure SQL Database Password** - Previously exposed in `appsettings.json`
4. **Azure Blob Storage Key** - Previously exposed in `appsettings.json`

## Setup Instructions

### 1. Create Environment File

Copy the example environment file and fill in your actual credentials:

```bash
# From project root
cp .env.example .env

# OR from docker compose directory
cd infra/dockerCompose
cp .env.example .env
```

### 2. Configure Credentials

Edit the `.env` file and replace all placeholder values with your actual credentials:

```bash
# Anthropic API Configuration
ANTHROPIC_API_KEY=your_actual_anthropic_api_key

# AWS Configuration
AWS_ACCESS_KEY_ID=your_actual_aws_access_key
AWS_SECRET_ACCESS_KEY=your_actual_aws_secret_key
AWS_REGION=ap-south-1
AWS_DEFAULT_REGION=ap-south-1

# Database Configuration
ConnectionStrings__DefaultConnection=Server=tcp:blackhatbadshah.database.windows.net,...

# Azure Blob Storage Configuration
AzureBlob__ConnectionString=DefaultEndpointsProtocol=https;AccountName=...
```

### 3. Verify .gitignore

Ensure your `.env` file is never committed to version control. The `.gitignore` file has been updated to exclude:
- `.env`
- `.env.*`
- `.env.local`
- `.env.*.local`

But allows:
- `.env.example`

### 4. Running with Docker Compose

Docker Compose will automatically load environment variables from the `.env` file in the same directory:

```bash
cd infra/dockerCompose
docker-compose up -d
```

## Important Security Notes

1. **NEVER commit the `.env` file** - It contains sensitive credentials
2. **Rotate exposed credentials immediately** - The following credentials were previously exposed in version control and should be rotated:
   - Anthropic API key (starts with `sk-ant-api03-`)
   - AWS Access Key ID (starts with `AKIA`)
   - Azure SQL Database password
   - Azure Blob Storage account key

3. **Use different credentials for different environments** - Development, staging, and production should each have their own credentials

4. **Limit credential permissions** - Follow the principle of least privilege:
   - AWS: Only grant necessary S3 and Textract permissions
   - Azure: Limit database user permissions to required operations
   - API Keys: Use different keys for different services if possible

## Credential Rotation

If credentials are compromised:

1. **Immediately revoke/rotate** the exposed credentials
2. **Update** your `.env` file with new credentials
3. **Restart** all services to pick up new credentials
4. **Audit** access logs to check for unauthorized usage

## Additional Security Recommendations

1. Consider using a secrets management service:
   - Azure Key Vault
   - AWS Secrets Manager
   - HashiCorp Vault

2. Enable audit logging for all services

3. Implement IP whitelisting where possible

4. Use managed identities when running on cloud platforms

5. Regularly review and rotate credentials

## Questions?

For security concerns or questions, contact your security team immediately.
