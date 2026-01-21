# Privacy Policy

This document describes how Blackhatbadshah handles user data, log data, and third-party integrations.

## What Data Is Collected

- **User Account Data:**
  - Email, name, and authentication info (via Keycloak)
  - Subscription and payment info (via Razorpay)
- **Log Data:**
  - Log files streamed from user servers via BHB Worker
  - System metrics (CPU, memory, disk, network)
- **AI Analysis Data:**
  - Log content sent to AI service for analysis
  - Analysis results and generated reports

## How Data Is Stored

- **Database:**
  - User accounts, subscriptions, worker registrations, and report metadata are stored in Azure SQL
- **Blob Storage:**
  - Uploaded logs and generated reports are stored in Azure Blob Storage
- **AWS Textract:**
  - Document processing uses AWS Textract; processed data is stored in S3

## Third-Party Integrations

- **Keycloak:** Used for authentication and user management
- **Razorpay:** Used for payment processing; payment data is handled securely via Razorpay APIs
- **Anthropic Claude:** Log data may be sent to Anthropic for AI analysis
- **AWS Textract:** Used for document OCR

## Data Retention & Deletion

- Users can delete their reports and logs at any time via the dashboard
- Deleted data is removed from both the database and blob storage
- Payment and authentication data is managed by Razorpay and Keycloak respectively

## User Rights

- Users can request deletion of their account and associated data
- Users can export their reports and logs
- No user data is shared with third parties except for payment and AI analysis as described above

## Security Practices

- All credentials and secrets are managed via environment variables (see SECURITY.md)
- Sensitive data is encrypted in transit (HTTPS) and at rest (Azure, AWS)
- API keys and credentials are never stored in source control

## Contact

For privacy-related questions or requests, contact support via [github.com/ersanjayverma/Blackhatbadshah/issues](https://github.com/ersanjayverma/Blackhatbadshah/issues)
