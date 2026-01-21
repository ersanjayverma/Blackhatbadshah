# Blackhatbadshah

Multi-service AI-powered log analysis platform with .NET backend/frontend, Python AI service, and cross-platform worker agent.

## Architecture

This application consists of:

- **Frontend** (.NET 10.0 Blazor) - Web interface
- **Backend** (.NET 10.0 API) - REST API with Azure SQL and Blob Storage
- **AI Service** (Python FastAPI) - LangGraph-based AI agent with Claude integration
- **Worker Agent** (.NET 8.0) - Cross-platform log streaming agent (see `worker/bhbworker`)

## Prerequisites

- .NET 10.0 SDK (backend/frontend)
- .NET 8.0 SDK (worker agent)
- Python 3.11 (AI service)
- Valid credentials for:
  - Anthropic API (Claude)
  - AWS (Textract)
  - Azure SQL Database
  - Azure Blob Storage
  - Razorpay (payments)
  - Keycloak (auth)

## Quick Start

1. Copy `.env.example` to `.env` and fill in your credentials
2. Start backend, frontend, and AI service (see below)
3. Register and install worker agent (see `worker/bhbworker/README.md`)

## Service Ports

- **Frontend**: http://localhost:7001
- **Backend API**: http://localhost:5092
- **AI Service**: http://localhost:8501

## Configuration

All sensitive configuration is managed via environment variables and config files:
- `.env.example` - Template for required environment variables
- `appsettings.json` (worker agent) - Stores API key and worker ID
- See `SECURITY.md` for best practices

## Development

### Backend (.NET)
```bash
cd backend/backend
dotnet restore
dotnet run
```

### Frontend (.NET)
```bash
cd frontend/frontend
dotnet restore
dotnet run
```

### AI Service (Python)
```bash
cd ai
pip install -r requirements.txt
uvicorn main:api --reload
```

### Worker Agent (.NET)
See `worker/bhbworker/README.md` for build, install, and configuration instructions.

## Security & Privacy

- All credentials and secrets are managed via environment variables and config files
- Never commit `.env` or secret config files to version control
- See `SECURITY.md` for credential management and rotation
- See `PRIVACY.md` for data handling and user privacy policy

## Project Structure

```
.
├── backend/          # .NET 10.0 Backend API
├── frontend/         # .NET 10.0 Blazor Frontend
├── ai/               # Python FastAPI AI Service
├── worker/           # .NET 8.0 Worker Agent (cross-platform)
│   └── bhbworker/    # Worker agent source and install guide
├── shared/           # Shared code
├── infra/            # Infrastructure configs
│   ├── dockerCompose/
│   └── nginxConfs/
├── setup.sh          # Automated setup script
├── SECURITY.md       # Security documentation
├── PRIVACY.md        # Privacy policy
```

## Technologies

- **.NET 10.0** - Backend and frontend
- **Python 3.11** - AI service
- **LangGraph** - AI agent orchestration
- **Anthropic Claude** - Language model
- **Azure SQL** - Database
- **Azure Blob Storage** - File storage
- **AWS Textract** - Document processing
- **Docker** - Containerization

## Troubleshooting

### Services won't start
- Check that all credentials are correctly set in `.env`
- Verify Docker daemon is running
- Check logs: `docker-compose logs -f`

### Database connection issues
- Verify Azure SQL firewall rules allow your IP
- Test connection string separately
- Check that database user has proper permissions

### AI service errors
- Verify Anthropic API key is valid
- Check API quota limits
- Review logs: `docker-compose logs blackhat-ai`

## License

Private project - All rights reserved