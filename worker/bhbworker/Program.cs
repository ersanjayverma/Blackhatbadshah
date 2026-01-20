using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR.Client;
using BHBWorker;

// Determine the base path for configuration
var basePath = AppContext.BaseDirectory;

// When running as single-file, use the directory containing the executable
if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
{
    basePath = Directory.GetCurrentDirectory();
}

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(basePath)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables("BHB_")
    .AddCommandLine(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    builder.Logging.AddSystemdConsole();
}

// Add platform-specific service support
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "BHBWorker";
    });
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    builder.Services.AddSystemd();
}

builder.Services.AddHostedService<LogPusherService>();

var host = builder.Build();

// Print startup banner
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("========================================");
logger.LogInformation("BHB Worker v1.0.0");
logger.LogInformation("Platform: {Platform}", RuntimeInformation.OSDescription);
logger.LogInformation("Config Path: {Path}", basePath);
logger.LogInformation("========================================");

await host.RunAsync();

