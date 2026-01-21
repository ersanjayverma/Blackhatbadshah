# BHB Worker

A lightweight log streaming agent that connects to BlackHatBadshah for real-time log analysis.

## Features

- Real-time log streaming via SignalR
- System metrics monitoring (CPU, Memory, Disk, Network)
- Process monitoring and management
- Cross-platform support (Linux & Windows)
- Runs as a system service

## Prerequisites

- .NET 8.0 Runtime (or use the pre-built binaries)
- Access to log files you want to monitor
- Network access to `api.blackhatbadshah.com`

## Installation

### Step 1: Register a Worker

Before installing the agent, you need to register a worker in the BlackHatBadshah dashboard:

1. Go to [blackhatbadshah.com](https://blackhatbadshah.com)
2. Navigate to **My Workers** page
3. Click **Initialize Configuration** (first time only)
4. Click **Register New Worker**
5. Enter a name for your worker
6. **Save the API Key** - it will only be shown once!
7. Note the **Worker ID** (GUID format)

### Step 2: Download the Worker

#### Option A: Pre-built Binaries (Recommended)

Download the appropriate binary for your platform:

**Linux (x64):**
```bash
# Create installation directory
sudo mkdir -p /opt/bhbworker

# Download and extract
curl -L https://github.com/ersanjayverma/Blackhatbadshah/releases/latest/download/bhbworker-linux-x64.tar.gz | sudo tar -xz -C /opt/bhbworker

# Make executable
sudo chmod +x /opt/bhbworker/bhbworker
```

**Windows (x64):**
```powershell
# Create installation directory
New-Item -ItemType Directory -Force -Path "C:\Program Files\BHBWorker"

# Download (adjust URL for actual release)
Invoke-WebRequest -Uri "https://github.com/ersanjayverma/Blackhatbadshah/releases/latest/download/bhbworker-win-x64.zip" -OutFile "$env:TEMP\bhbworker.zip"

# Extract
Expand-Archive -Path "$env:TEMP\bhbworker.zip" -DestinationPath "C:\Program Files\BHBWorker" -Force
```

#### Option B: Build from Source

```bash
cd worker/bhbworker

# Linux
dotnet publish -c Release -r linux-x64 --self-contained -o ./publish/linux

# Windows
dotnet publish -c Release -r win-x64 --self-contained -o ./publish/windows
```

### Step 3: Configure the Worker

Create or edit `appsettings.json` in the installation directory:

**Linux:** `/opt/bhbworker/appsettings.json`
**Windows:** `C:\Program Files\BHBWorker\appsettings.json`

```json
{
  "LiveLogHub": {
    "Url": "https://api.blackhatbadshah.com/hubs/livelog",
    "ApiKey": "YOUR_API_KEY_HERE",
    "WorkerId": "YOUR_WORKER_ID_HERE",
    "Model": "together-qwen",
    "ReconnectDelayMs": 5000
  },
  "LogReader": {
    "LogPaths": [
      "/var/log/syslog",
      "/var/log/auth.log"
    ],
    "BatchSize": 50,
    "BatchDelayMs": 100
  }
}
```

**Configuration Options:**

| Setting | Description | Default |
|---------|-------------|---------|
| `LiveLogHub:Url` | SignalR hub URL | `https://api.blackhatbadshah.com/hubs/livelog` |
| `LiveLogHub:ApiKey` | Your worker API key from dashboard | Required |
| `LiveLogHub:WorkerId` | Your worker ID from dashboard | Required |
| `LiveLogHub:Model` | AI model for log analysis | `together-qwen` |
| `LiveLogHub:ReconnectDelayMs` | Reconnection delay on disconnect | `5000` |
| `LogReader:LogPaths` | Array of log file paths to monitor | Platform specific |
| `LogReader:BatchSize` | Lines to batch before sending | `50` |
| `LogReader:BatchDelayMs` | Delay between batches | `100` |

**Common Log Paths:**

Linux:
```json
"LogPaths": [
  "/var/log/syslog",
  "/var/log/auth.log",
  "/var/log/nginx/access.log",
  "/var/log/nginx/error.log",
  "/var/log/apache2/access.log",
  "/var/log/apache2/error.log"
]
```

Windows:
```json
"LogPaths": [
  "C:\\inetpub\\logs\\LogFiles\\W3SVC1\\u_ex*.log",
  "C:\\Windows\\System32\\LogFiles\\*.log"
]
```

### Step 4: Install as a Service

#### Linux (systemd)

Create the service file:

```bash
sudo nano /etc/systemd/system/bhbworker.service
```

Add the following content:

```ini
[Unit]
Description=BHB Worker - Log Streaming Agent
After=network.target

[Service]
Type=notify
WorkingDirectory=/opt/bhbworker
ExecStart=/opt/bhbworker/bhbworker
Restart=always
RestartSec=10
User=root
Environment=DOTNET_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

Enable and start the service:

```bash
sudo systemctl daemon-reload
sudo systemctl enable bhbworker
sudo systemctl start bhbworker
```

Check status:
```bash
sudo systemctl status bhbworker
sudo journalctl -u bhbworker -f
```

#### Windows (Windows Service)

Install as a Windows service using PowerShell (Run as Administrator):

```powershell
# Create the service
New-Service -Name "BHBWorker" `
  -BinaryPathName '"C:\Program Files\BHBWorker\bhbworker.exe"' `
  -DisplayName "BHB Worker" `
  -Description "BlackHatBadshah Log Streaming Agent" `
  -StartupType Automatic

# Start the service
Start-Service -Name "BHBWorker"

# Check status
Get-Service -Name "BHBWorker"
```

To view logs on Windows:
```powershell
Get-EventLog -LogName Application -Source "BHBWorker" -Newest 50
```

### Step 5: Verify Connection

1. Check the worker logs:
   - Linux: `sudo journalctl -u bhbworker -f`
   - Windows: Event Viewer > Application Log

2. Go to [blackhatbadshah.com/workers](https://blackhatbadshah.com/workers)

3. Your worker should appear as "Online" with a green status indicator

## Environment Variables

You can also configure the worker using environment variables (prefix with `BHB_`):

```bash
export BHB_LiveLogHub__ApiKey="your-api-key"
export BHB_LiveLogHub__WorkerId="your-worker-id"
export BHB_LogReader__LogPaths__0="/var/log/syslog"
```

## Troubleshooting

### Worker not connecting

1. Check network connectivity:
   ```bash
   curl -I https://api.blackhatbadshah.com/health
   ```

2. Verify API key and Worker ID are correct

3. Check firewall rules allow outbound HTTPS (443)

### Permission denied reading logs

Linux:
```bash
# Add user to appropriate groups
sudo usermod -aG adm root
# Or run as root (already configured in systemd)
```

Windows:
- Run the service as Administrator or a user with log access

### High CPU/Memory usage

Adjust batch settings in `appsettings.json`:
```json
"LogReader": {
  "BatchSize": 25,
  "BatchDelayMs": 200
}
```

## Uninstallation

### Linux
```bash
sudo systemctl stop bhbworker
sudo systemctl disable bhbworker
sudo rm /etc/systemd/system/bhbworker.service
sudo rm -rf /opt/bhbworker
sudo systemctl daemon-reload
```

### Windows
```powershell
Stop-Service -Name "BHBWorker"
sc.exe delete "BHBWorker"
Remove-Item -Recurse -Force "C:\Program Files\BHBWorker"
```

## Support

For issues or questions, visit [github.com/ersanjayverma/Blackhatbadshah/issues](https://github.com/ersanjayverma/Blackhatbadshah/issues)
