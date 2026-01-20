using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using backend.Data;
using backend.Data.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Handlers;

public class WorkerKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "WorkerKey";
    public const string HeaderName = "X-Worker-Key";
}

public class WorkerKeyAuthenticationHandler : AuthenticationHandler<WorkerKeyAuthenticationOptions>
{
    private readonly AppDbContext _db;

    public WorkerKeyAuthenticationHandler(
        IOptionsMonitor<WorkerKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext db)
        : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(WorkerKeyAuthenticationOptions.HeaderName, out var apiKeyValue))
        {
            return AuthenticateResult.NoResult();
        }

        var apiKey = apiKeyValue.ToString();
        if (string.IsNullOrEmpty(apiKey))
        {
            return AuthenticateResult.Fail("API key is empty");
        }

        // Hash the provided key
        var hashedKey = HashApiKey(apiKey);

        // Find worker with matching hash and active status
        var worker = await _db.WorkerAgents
            .FirstOrDefaultAsync(w => w.ApiKeyHash == hashedKey && w.Status == WorkerAgentStatus.Active);

        if (worker == null)
        {
            return AuthenticateResult.Fail("Invalid or revoked API key");
        }

        // Update last seen timestamp
        worker.LastSeenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Build claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, $"worker:{worker.Id}"),
            new Claim("sub", $"worker:{worker.Id}"),
            new Claim("workerId", worker.Id.ToString()),
            new Claim("workspaceId", worker.WorkspaceId.ToString()),
            new Claim(ClaimTypes.Role, "Worker"),
            new Claim("workerName", worker.Name)
        };

        var identity = new ClaimsIdentity(claims, WorkerKeyAuthenticationOptions.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, WorkerKeyAuthenticationOptions.SchemeName);

        return AuthenticateResult.Success(ticket);
    }

    public static string HashApiKey(string apiKey)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToBase64String(hashedBytes);
    }

    public static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
