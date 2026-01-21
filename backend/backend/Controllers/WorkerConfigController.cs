using backend.Data;
using backend.Data.Entities;
using backend.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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

    private string? GetUserId()
    {
        // Try multiple claim types that Keycloak might use
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub")
                     ?? User.FindFirstValue("preferred_username");
        
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Could not determine user ID from claims. Available claims: {Claims}",
                string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
        }
        
        return userId;
    }

    /// <summary>
    /// Get current user's worker configuration
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetConfig()
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("GetConfig: User ID is null or empty");
                return Unauthorized(new { error = "Unable to determine user identity" });
            }

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting worker config");
            return StatusCode(500, new { error = "Failed to get worker config", details = ex.Message });
        }
    }

    /// <summary>
    /// Initialize worker configuration and generate PSK for first time
    /// </summary>
    [HttpPost("initialize")]
    public async Task<IActionResult> Initialize([FromBody] InitializeWorkerConfigRequest? request)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "Unable to determine user identity" });

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing worker config");
            return StatusCode(500, new { error = "Failed to initialize worker config", details = ex.Message });
        }
    }

    /// <summary>
    /// Rotate the user's PSK
    /// </summary>
    [HttpPost("rotate-psk")]
    public async Task<IActionResult> RotatePsk()
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "Unable to determine user identity" });

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rotating PSK");
            return StatusCode(500, new { error = "Failed to rotate PSK", details = ex.Message });
        }
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
WantedBy=multi-user.target";
    }

    private static string GenerateLinuxInstallCommands()
    {
        return @"# Step 1: Create installation directory
sudo mkdir -p /opt/bhbworker

# Step 2: Download the latest release
cd /opt/bhbworker
sudo curl -L https://github.com/ersanjayverma/Blackhatbadshah/releases/latest/download/bhbworker-linux-x64.tar.gz -o bhbworker.tar.gz
sudo tar -xzf bhbworker.tar.gz
sudo rm bhbworker.tar.gz
sudo chmod +x bhbworker

# Step 3: Create configuration file
sudo nano /opt/bhbworker/appsettings.json
# (Paste the configuration from Step 4 below)

# Step 4: Configuration template - save as appsettings.json:
# {
#   ""LiveLogHub"": {
#     ""Url"": ""https://api.blackhatbadshah.com/hubs/livelog"",
#     ""ApiKey"": ""YOUR_API_KEY_HERE"",
#     ""WorkerId"": ""YOUR_WORKER_ID_HERE"",
#     ""Model"": ""together-qwen"",
#     ""ReconnectDelayMs"": 5000
#   },
#   ""LogReader"": {
#     ""LogPaths"": [""/var/log/syslog"", ""/var/log/auth.log""],
#     ""BatchSize"": 50,
#     ""BatchDelayMs"": 100
#   }
# }

# Step 5: Install systemd service
sudo tee /etc/systemd/system/bhbworker.service > /dev/null << 'EOF'
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
EOF

# Step 6: Enable and start the service
sudo systemctl daemon-reload
sudo systemctl enable bhbworker
sudo systemctl start bhbworker

# Step 7: Verify it's running
sudo systemctl status bhbworker
sudo journalctl -u bhbworker -f";
    }

    private static string GenerateWindowsServiceCommands(string workerId)
    {
        return $@"# Run PowerShell as Administrator

# Step 1: Create installation directory
New-Item -ItemType Directory -Force -Path ""C:\Program Files\BHBWorker""

# Step 2: Download the latest release
# Download bhbworker-win-x64.zip from:
# https://github.com/ersanjayverma/Blackhatbadshah/releases/latest
# Extract to C:\Program Files\BHBWorker\

# Step 3: Create configuration file
# Create C:\Program Files\BHBWorker\appsettings.json with:
# {{
#   ""LiveLogHub"": {{
#     ""Url"": ""https://api.blackhatbadshah.com/hubs/livelog"",
#     ""ApiKey"": ""YOUR_API_KEY_HERE"",
#     ""WorkerId"": ""{workerId}"",
#     ""Model"": ""together-qwen"",
#     ""ReconnectDelayMs"": 5000
#   }},
#   ""LogReader"": {{
#     ""LogPaths"": [""C:\\inetpub\\logs\\LogFiles\\W3SVC1\\*.log""],
#     ""BatchSize"": 50,
#     ""BatchDelayMs"": 100
#   }}
# }}

# Step 4: Install as Windows Service
New-Service -Name ""BHBWorker"" `
  -BinaryPathName '""C:\Program Files\BHBWorker\bhbworker.exe""' `
  -DisplayName ""BHB Worker"" `
  -Description ""BlackHatBadshah Log Streaming Agent"" `
  -StartupType Automatic

# Step 5: Start the service
Start-Service -Name ""BHBWorker""

# Step 6: Verify it's running
Get-Service -Name ""BHBWorker""";
    }

    private static string GenerateDockerRunCommand(string workerId)
    {
        // Docker support removed - use native installation instead
        return @"Docker installation is not currently supported.

Please use the Linux or Windows native installation method.

For Linux: Use the systemd service installation
For Windows: Use the Windows Service installation";
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
