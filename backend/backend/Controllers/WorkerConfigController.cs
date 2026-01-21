using backend.Data;
using backend.Data.Entities;
using backend.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace backend.Controllers;

[ApiController]
[Route("api/worker-config")]
[Authorize]
public class WorkerConfigController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<WorkerConfigController> _logger;

    public WorkerConfigController(AppDbContext db, ILogger<WorkerConfigController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private string? GetUserId() => User.FindFirst("sub")?.Value;

    /// <summary>
    /// Get current user's worker configuration
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetConfig()
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var config = await _db.UserWorkerConfigs
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (config == null)
        {
            return Ok(new UserWorkerConfigResponse
            {
                HasConfig = false,
                WorkerCount = 0,
                MaxWorkers = 3
            });
        }

        var workerCount = await _db.WorkerAgents
            .CountAsync(w => w.CreatedByUserId == userId && w.Status == WorkerAgentStatus.Active);

        return Ok(new UserWorkerConfigResponse
        {
            HasConfig = true,
            ConfigId = config.Id,
            ConfigName = config.ConfigName,
            IsEnabled = config.IsEnabled,
            MaxWorkers = config.MaxWorkers,
            WorkerCount = workerCount,
            CreatedAt = config.CreatedAt,
            LastPskRotatedAt = config.LastPskRotatedAt,
            LastWorkerActivityAt = config.LastWorkerActivityAt
        });
    }

    /// <summary>
    /// Initialize worker configuration and generate PSK for first time
    /// </summary>
    [HttpPost("initialize")]
    public async Task<IActionResult> Initialize([FromBody] InitializeWorkerConfigRequest? request)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // Check if config already exists
        var existingConfig = await _db.UserWorkerConfigs
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (existingConfig != null)
        {
            return Conflict(new { error = "Worker configuration already exists. Use rotate-psk to generate a new key." });
        }

        // Generate PSK
        var psk = GeneratePsk();
        var pskHash = WorkerKeyAuthenticationHandler.HashApiKey(psk);

        var config = new UserWorkerConfig
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PskHash = pskHash,
            ConfigName = request?.ConfigName ?? "Default Configuration",
            MaxWorkers = 3, // Default limit - can be updated based on subscription
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.UserWorkerConfigs.Add(config);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Worker configuration initialized for user {UserId}", userId);

        return Ok(new InitializeWorkerConfigResponse
        {
            ConfigId = config.Id,
            Psk = psk, // Only returned ONCE
            Message = "Worker configuration initialized successfully. Save your PSK securely - it will not be shown again!"
        });
    }

    /// <summary>
    /// Rotate the user's PSK
    /// </summary>
    [HttpPost("rotate-psk")]
    public async Task<IActionResult> RotatePsk()
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var config = await _db.UserWorkerConfigs
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (config == null)
        {
            return NotFound(new { error = "Worker configuration not found. Initialize first." });
        }

        // Generate new PSK
        var psk = GeneratePsk();
        var pskHash = WorkerKeyAuthenticationHandler.HashApiKey(psk);

        config.PskHash = pskHash;
        config.LastPskRotatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("PSK rotated for user {UserId}", userId);

        return Ok(new RotatePskResponse
        {
            Psk = psk, // Only returned ONCE
            RotatedAt = config.LastPskRotatedAt.Value,
            Message = "PSK rotated successfully. Update your workers with the new PSK. The old PSK is now invalid."
        });
    }

    /// <summary>
    /// Update worker configuration settings
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> UpdateConfig([FromBody] UpdateWorkerConfigRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var config = await _db.UserWorkerConfigs
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (config == null)
        {
            return NotFound(new { error = "Worker configuration not found. Initialize first." });
        }

        if (!string.IsNullOrWhiteSpace(request.ConfigName))
        {
            config.ConfigName = request.ConfigName;
        }

        if (request.IsEnabled.HasValue)
        {
            config.IsEnabled = request.IsEnabled.Value;
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = "Configuration updated successfully" });
    }

    /// <summary>
    /// Get worker installation instructions for user
    /// </summary>
    [HttpGet("install-instructions")]
    public async Task<IActionResult> GetInstallInstructions()
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var config = await _db.UserWorkerConfigs
            .FirstOrDefaultAsync(c => c.UserId == userId);

        // Get or create a worker for instructions
        var worker = await _db.WorkerAgents
            .Where(w => w.CreatedByUserId == userId && w.Status == WorkerAgentStatus.Active)
            .FirstOrDefaultAsync();

        var workerIdPlaceholder = worker?.Id.ToString() ?? "<YOUR_WORKER_ID>";

        var instructions = new WorkerInstallInstructions
        {
            HasConfig = config != null,
            LinuxSystemdService = GenerateSystemdServiceContent(workerIdPlaceholder),
            LinuxInstallCommands = GenerateLinuxInstallCommands(),
            WindowsServiceCommands = GenerateWindowsServiceCommands(workerIdPlaceholder),
            DockerRunCommand = GenerateDockerRunCommand(workerIdPlaceholder),
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["BHB_API_URL"] = "https://api.blackhatbadshah.com",
                ["BHB_WORKER_ID"] = workerIdPlaceholder,
                ["BHB_WORKER_KEY"] = "<YOUR_WORKER_API_KEY>",
                ["BHB_LOG_PATHS"] = "/var/log/syslog,/var/log/auth.log"
            }
        };

        return Ok(instructions);
    }

    private static string GeneratePsk()
    {
        // Generate a 32-byte (256-bit) cryptographically secure key
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes);
    }

    private static string GenerateSystemdServiceContent(string workerId)
    {
        return $@"[Unit]
Description=BlackHatBadshah Worker Agent
After=network.target

[Service]
Type=simple
User=bhbworker
Group=bhbworker
WorkingDirectory=/opt/bhbworker
ExecStart=/opt/bhbworker/BHBWorker
Restart=always
RestartSec=10
Environment=BHB_API_URL=https://api.blackhatbadshah.com
Environment=BHB_WORKER_ID={workerId}
Environment=BHB_WORKER_KEY=<YOUR_WORKER_API_KEY>
Environment=BHB_LOG_PATHS=/var/log/syslog,/var/log/auth.log

# Security hardening
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
NoNewPrivileges=true
ReadWritePaths=/opt/bhbworker

[Install]
WantedBy=multi-user.target";
    }

    private static string GenerateLinuxInstallCommands()
    {
        return @"# Create user and directory
sudo useradd -r -s /bin/false bhbworker
sudo mkdir -p /opt/bhbworker
sudo chown bhbworker:bhbworker /opt/bhbworker

# Download and extract worker
cd /opt/bhbworker
sudo curl -L https://github.com/ersanjayverma/Blackhatbadshah/releases/latest/download/bhbworker-linux-x64.tar.gz | sudo tar xz

# Install systemd service
sudo cp /opt/bhbworker/bhbworker.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable bhbworker
sudo systemctl start bhbworker

# Check status
sudo systemctl status bhbworker";
    }

    private static string GenerateWindowsServiceCommands(string workerId)
    {
        return $@"# Run as Administrator in PowerShell

# Create service
sc.exe create BHBWorker binPath= ""C:\BHBWorker\BHBWorker.exe"" start= auto
sc.exe description BHBWorker ""BlackHatBadshah Worker Agent""

# Set environment variables
setx BHB_API_URL ""https://api.blackhatbadshah.com"" /M
setx BHB_WORKER_ID ""{workerId}"" /M
setx BHB_WORKER_KEY ""<YOUR_WORKER_API_KEY>"" /M
setx BHB_LOG_PATHS ""C:\Windows\System32\winevt\Logs"" /M

# Start service
sc.exe start BHBWorker";
    }

    private static string GenerateDockerRunCommand(string workerId)
    {
        return $@"docker run -d \
  --name bhbworker \
  --restart unless-stopped \
  -e BHB_API_URL=https://api.blackhatbadshah.com \
  -e BHB_WORKER_ID={workerId} \
  -e BHB_WORKER_KEY=<YOUR_WORKER_API_KEY> \
  -e BHB_LOG_PATHS=/var/log/syslog,/var/log/auth.log \
  -v /var/log:/var/log:ro \
  ghcr.io/ersanjayverma/bhbworker:latest";
    }
}

// DTOs
public class UserWorkerConfigResponse
{
    public bool HasConfig { get; set; }
    public Guid? ConfigId { get; set; }
    public string? ConfigName { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int MaxWorkers { get; set; }
    public int WorkerCount { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastPskRotatedAt { get; set; }
    public DateTime? LastWorkerActivityAt { get; set; }
}

public class InitializeWorkerConfigRequest
{
    public string? ConfigName { get; set; }
}

public class InitializeWorkerConfigResponse
{
    public Guid ConfigId { get; set; }
    public string Psk { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class RotatePskResponse
{
    public string Psk { get; set; } = string.Empty;
    public DateTime RotatedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class UpdateWorkerConfigRequest
{
    public string? ConfigName { get; set; }
    public bool? IsEnabled { get; set; }
}

public class WorkerInstallInstructions
{
    public bool HasConfig { get; set; }
    public string LinuxSystemdService { get; set; } = string.Empty;
    public string LinuxInstallCommands { get; set; } = string.Empty;
    public string WindowsServiceCommands { get; set; } = string.Empty;
    public string DockerRunCommand { get; set; } = string.Empty;
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
}
