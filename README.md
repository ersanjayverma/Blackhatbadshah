# Blackhatbadshah

Multi-service AI-powered application with .NET backend/frontend and Python AI service using Anthropic Claude.

## Architecture

This application consists of three main services:

- **Frontend** (.NET 10.0 Blazor) - Web interface
- **Backend** (.NET 10.0 API) - REST API with Azure SQL and Blob Storage
- **AI Service** (Python FastAPI) - LangGraph-based AI agent with Claude integration

## Prerequisites

- Docker and Docker Compose
- Valid credentials for:
  - Anthropic API (Claude)
  - AWS (for Textract)
  - Azure SQL Database
  - Azure Blob Storage

## Quick Start

### Option 1: Automated Setup (Recommended)

Run the interactive setup script:

```bash
./setup.sh
```

This will guide you through configuring all required credentials.

### Option 2: Manual Setup

1. Copy the environment template:
   ```bash
   cp infra/dockerCompose/.env.example infra/dockerCompose/.env
   ```

2. Edit `infra/dockerCompose/.env` and fill in your credentials

3. Start the services:
   ```bash
   cd infra/dockerCompose
   docker-compose up -d
   ```

## Service Ports

- **Frontend**: http://localhost:7001
- **Backend API**: http://localhost:5092
- **AI Service**: http://localhost:8501

## Configuration

All sensitive configuration is managed through environment variables. See:
- `.env.example` - Template for required environment variables
- `SECURITY.md` - Security guidelines and credential management

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

## Security

⚠️ **Important**: This repository previously contained exposed credentials. If you have access to the old commits:

1. **Immediately rotate** all exposed credentials (see SECURITY.md)
2. Never commit `.env` files to version control
3. Use different credentials for development and production

See `SECURITY.md` for detailed security guidelines.

## Project Structure

```
.
├── backend/          # .NET 10.0 Backend API
├── frontend/         # .NET 10.0 Blazor Frontend
├── ai/               # Python FastAPI AI Service
├── shared/           # Shared code
├── infra/            # Infrastructure configs
│   ├── dockerCompose/
│   └── nginxConfs/
├── setup.sh          # Automated setup script
└── SECURITY.md       # Security documentation
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