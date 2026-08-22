# Security Policy

## Scope

Blackhatbadshah is intended to be developed in public wherever the material is non-personal, non-confidential, and safe to disclose.

Do not publish customer data, private logs, credentials, access tokens, private infrastructure details, personal information, or proprietary incident data.

## Secrets

Secrets must never be committed to source control.

Use environment variables or a dedicated secret manager for:

- database connection strings
- API keys and LLM credentials
- cloud credentials
- payment credentials
- Keycloak client secrets
- SMTP credentials
- vector database credentials
- worker authentication tokens

Tracked configuration files contain placeholders only.

## If a secret was exposed

Treat it as compromised even if the exposure was brief.

1. Revoke or rotate the credential immediately.
2. Replace the tracked value with a placeholder.
3. Audit the affected service for unauthorized use.
4. Review Git history and hosting logs.
5. If necessary, rewrite repository history to remove the secret completely.

Removing a secret from the latest commit is **not sufficient** because Git history may still contain the old value.

## Reporting a vulnerability

For a suspected security vulnerability, avoid posting exploit details or sensitive evidence in a public issue.

Provide:

- affected component
- impact
- reproduction conditions
- safe evidence
- suggested mitigation, if known

Do not include credentials, customer information, private logs, or production data.

## Public-by-default rule

The project favors transparent engineering. Architecture, APIs, design decisions, benchmarks, sanitized diagnostics, and reproducible technical findings should be public when they contain no personal or confidential information.

The following stay private:

- credentials and secrets
- customer or employee information
- private infrastructure access details
- proprietary production data
- security-sensitive material that could enable unauthorized access
