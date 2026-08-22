# Blackhatbadshah

> **We diagnose and fix invisible technical failures that cost companies money.**

Blackhatbadshah is an engineering lab focused on **AI-powered observability, system diagnostics, developer tooling, and practical automation**.

The flagship project is a production-oriented platform for turning application and infrastructure logs into actionable technical intelligence.

## Why Blackhatbadshah?

Most systems don't fail loudly. They degrade through slow requests, noisy logs, hidden dependency failures, resource pressure, and configuration drift.

Blackhatbadshah is built around one principle:

**Don't just collect telemetry. Explain what is actually going wrong.**

## What we're building

- 🔎 **AI-assisted diagnosis** — correlate logs and failures instead of searching them manually
- 🧠 **Agentic analysis** — LangGraph-based reasoning workflows
- ⚙️ **.NET-first engineering** — high-performance services and tooling
- 📡 **Cross-platform telemetry** — lightweight worker/agent architecture
- 🐳 **Containerized deployment** — reproducible local and server environments
- 🔐 **Security-conscious operations** — secrets stay out of source control

## Architecture

```text
                 ┌──────────────────────┐
                 │   Blazor Web UI      │
                 │      .NET 10         │
                 └──────────┬───────────┘
                            │
                            ▼
                 ┌──────────────────────┐
                 │    Backend API       │
                 │      .NET 10         │
                 └───────┬───────┬──────┘
                         │       │
              ┌──────────┘       └──────────┐
              ▼                             ▼
     ┌─────────────────┐          ┌─────────────────┐
     │   AI Service    │          │  Worker Agent   │
     │ Python +        │          │     .NET 8      │
     │ LangGraph       │          │ Cross-platform  │
     └─────────────────┘          └─────────────────┘
```

## Technology

| Area | Technology |
|---|---|
| Frontend | .NET 10 / Blazor |
| Backend | ASP.NET Core / .NET 10 |
| AI | Python / FastAPI / LangGraph |
| LLM | Anthropic Claude |
| Worker | .NET 8 |
| Database | Azure SQL |
| Storage | Azure Blob Storage |
| Document processing | AWS Textract |
| Deployment | Docker |

## Repository layout

```text
.
├── backend/          # .NET 10 API
├── frontend/         # .NET 10 Blazor application
├── ai/               # AI analysis service
├── worker/           # Cross-platform telemetry worker
├── shared/           # Shared components
├── infra/            # Docker and reverse-proxy configuration
├── setup.sh          # Development setup
├── SECURITY.md       # Security guidance
└── PRIVACY.md        # Privacy documentation
```

## Quick start

### Prerequisites

- .NET 10 SDK
- .NET 8 SDK
- Python 3.11
- Docker
- Required service credentials

### Configure

```bash
cp .env.example .env
```

Add your credentials to `.env` and **never commit secrets**.

### Backend

```bash
cd backend/backend
dotnet restore
dotnet run
```

### Frontend

```bash
cd frontend/frontend
dotnet restore
dotnet run
```

### AI service

```bash
cd ai
pip install -r requirements.txt
uvicorn main:api --reload
```

## Development philosophy

Blackhatbadshah favors:

- measurable engineering over hype
- root-cause analysis over symptom treatment
- simple systems over unnecessary complexity
- automation where it removes repetitive work
- security and accountability by default

## Roadmap

- [ ] Stronger log-to-root-cause correlation
- [ ] System health scoring
- [ ] Cross-service incident timelines
- [ ] More autonomous diagnostic workflows
- [ ] Open-source developer utilities
- [ ] Production-grade observability integrations

## Contributing

Useful bug reports, diagnostic ideas, documentation improvements, and focused pull requests are welcome.

If you find a real failure mode that deserves better tooling, open an issue and explain the problem, evidence, and expected behavior.

## Security

Do not publish credentials, API keys, private logs, customer data, or other sensitive material in issues or pull requests.

See [SECURITY.md](SECURITY.md) for reporting and credential-handling guidance.

## License

Private project — All rights reserved.

---

**Blackhatbadshah**  
Engineering the tools that find the failures other tools miss.
