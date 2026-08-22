# Security Policy

Blackhatbadshah is developed publicly wherever the material is non-personal, non-confidential, and safe to disclose.

## Reporting a vulnerability

Do not publish exploitable details or sensitive evidence in a public issue before the maintainer has had a chance to assess the problem.

Use GitHub's private vulnerability reporting/security-advisory mechanism when available. If private reporting is unavailable, open a minimal issue asking for maintainer attention without exploit details.

When safe to share privately, include:

- affected component
- affected version or commit
- impact
- reproduction conditions
- evidence
- suggested mitigation, if known

## Secrets and private data

Never commit:

- credentials or API keys
- access tokens
- database connection strings
- cloud credentials
- customer or employee information
- private production logs
- private infrastructure access details
- proprietary incident data

If a secret is exposed, treat it as compromised: revoke/rotate it, remove it from the working tree, audit use, and review Git history. Removing it only from the latest commit is not sufficient.

## Security scope

Security concerns include authentication or authorization flaws, unsafe parsing, memory-safety issues, privilege-boundary failures, process-isolation failures, secret exposure, and vulnerabilities in repository tooling or deployment configuration.

## Disclosure principle

The goal is responsible remediation and useful technical learning—not public blame or unnecessary exposure.
