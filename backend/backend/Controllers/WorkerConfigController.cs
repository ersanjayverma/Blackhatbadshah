using backend.Common;
using backend.Data;
using backend.Data.Entities;
using backend.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using shared.Dto;
using System.Security.Cryptography;

namespace backend.Controllers;

[Route("api/worker-config")]
[Authorize]
public class WorkerConfigController : BaseApiController
{
    private readonly AppDbContext _db;
    private readonly ILogger<WorkerConfigController> _logger;

    public WorkerConfigController(AppDbContext db, ILogger<WorkerConfigController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetConfig()
    {
        try
        {
            if (!TryGetUserId(out var userId))
            {
                _logger.LogWarning("GetConfig: User ID is null or empty");
                return UnauthorizedWithError(ErrorMessages.Unauthorized);
            }

            var config = await _db.UserWorkerConfigs
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (config == null)
            {
                // Default to Free plan if no config
                var plan = HttpContext.RequestServices.GetService<backend.Configuration.PlansConfiguration>()?.Free;
                return Ok(new UserWorkerConfigResponse
                {
                    HasConfig = false,
                    WorkerCount = 0,
                    MaxWorkers = plan?.MaxWorkers ?? 3
                });
            }

            // Determine plan by user (stub: always Free, can be extended)
            var planConfig = HttpContext.RequestServices.GetService<backend.Configuration.PlansConfiguration>()?.Free;
            if (config.MaxWorkers != (planConfig?.MaxWorkers ?? 3))
            {
                config.MaxWorkers = planConfig?.MaxWorkers ?? 3;
                await _db.SaveChangesAsync();
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

    [HttpPost("initialize")]
    public async Task<IActionResult> Initialize([FromBody] InitializeWorkerConfigRequest? request)
    {
        try
        {
            if (!TryGetUserId(out var userId))
                return UnauthorizedWithError(ErrorMessages.Unauthorized);

            var existingConfig = await _db.UserWorkerConfigs
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (existingConfig != null)
                return ConflictWithError(ErrorMessages.WorkerConfigAlreadyExists);

            // Generate PSK
            var psk = GeneratePsk();
            var pskHash = WorkerKeyAuthenticationHandler.HashApiKey(psk);

            var config = new UserWorkerConfig
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PskHash = pskHash,
                ConfigName = request?.ConfigName ?? Defaults.DefaultWorkerConfigName,
                MaxWorkers = Defaults.DefaultMaxWorkers,
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

    [HttpPost("rotate-psk")]
    public async Task<IActionResult> RotatePsk()
    {
        try
        {
            if (!TryGetUserId(out var userId))
                return UnauthorizedWithError(ErrorMessages.Unauthorized);

            var config = await _db.UserWorkerConfigs
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (config == null)
                return NotFoundWithError(ErrorMessages.WorkerConfigNotFound);

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

    [HttpPut]
    public async Task<IActionResult> UpdateConfig([FromBody] UpdateWorkerConfigRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var config = await _db.UserWorkerConfigs
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (config == null)
            return NotFoundWithError(ErrorMessages.WorkerConfigNotFound);

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

    [HttpGet("install-instructions")]
    public async Task<IActionResult> GetInstallInstructions()
    {
        if (!TryGetUserId(out var userId))
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
        return @"Docker installation is not currently supported.

Please use the Linux or Windows native installation method.

For Linux: Use the systemd service installation
For Windows: Use the Windows Service installation";
    }
}
