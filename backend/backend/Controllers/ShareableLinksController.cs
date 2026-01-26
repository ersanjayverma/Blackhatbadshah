using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using backend.Data;
using backend.Data.Entities;
using backend.Services;
using shared.Dto;

namespace backend.Controllers;

[ApiController]
[Route("api/share")]
public class ShareableLinksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly string _storageRoot;

    public ShareableLinksController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
        _storageRoot = config["Storage:RootPath"]
            ?? throw new InvalidOperationException("Storage:RootPath not configured");
    }

    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub");

    /// <summary>
    /// Get all shareable links for the current user
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetLinks()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var links = await _db.ShareableLinks
            .Include(l => l.Report)
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new ShareableLinkDto
            {
                Id = l.Id,
                ReportId = l.ReportId,
                ReportTitle = l.Report.Title,
                Token = l.Token,
                ShareUrl = $"{baseUrl}/shared/{l.Token}",
                HasPassword = l.PasswordHash != null,
                ExpiresAt = l.ExpiresAt,
                AccessCount = l.AccessCount,
                MaxAccesses = l.MaxAccesses,
                IsActive = l.IsActive,
                CreatedAt = l.CreatedAt,
                LastAccessedAt = l.LastAccessedAt
            })
            .ToListAsync();

        return Ok(links);
    }

    /// <summary>
    /// Create a shareable link for a report
    /// </summary>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateLink([FromBody] CreateShareableLinkRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var report = await _db.Reports.FirstOrDefaultAsync(r => r.Id == request.ReportId && r.UserId == userId);
        if (report == null) return NotFound(new { error = "Report not found" });

        // Generate unique token
        var token = GenerateToken();

        string? passwordHash = null;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        var link = new ShareableLink
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ReportId = request.ReportId,
            Token = token,
            PasswordHash = passwordHash,
            ExpiresAt = request.ExpiresInHours.HasValue
                ? DateTime.UtcNow.AddHours(request.ExpiresInHours.Value)
                : null,
            MaxAccesses = request.MaxAccesses,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.ShareableLinks.Add(link);
        await _db.SaveChangesAsync();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        return Ok(new ShareableLinkDto
        {
            Id = link.Id,
            ReportId = link.ReportId,
            ReportTitle = report.Title,
            Token = link.Token,
            ShareUrl = $"{baseUrl}/shared/{link.Token}",
            HasPassword = link.PasswordHash != null,
            ExpiresAt = link.ExpiresAt,
            AccessCount = link.AccessCount,
            MaxAccesses = link.MaxAccesses,
            IsActive = link.IsActive,
            CreatedAt = link.CreatedAt
        });
    }

    /// <summary>
    /// Delete a shareable link
    /// </summary>
    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLink(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var link = await _db.ShareableLinks.FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId);
        if (link == null) return NotFound();

        _db.ShareableLinks.Remove(link);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Toggle link active status
    /// </summary>
    [Authorize]
    [HttpPatch("{id:guid}/toggle")]
    public async Task<IActionResult> ToggleLink(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var link = await _db.ShareableLinks.FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId);
        if (link == null) return NotFound();

        link.IsActive = !link.IsActive;
        await _db.SaveChangesAsync();

        return Ok(new { isActive = link.IsActive });
    }

    /// <summary>
    /// Access a shared report (public endpoint)
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{token}")]
    public async Task<IActionResult> AccessSharedReport(string token, [FromQuery] string? password = null)
    {
        var link = await _db.ShareableLinks
            .Include(l => l.Report)
            .ThenInclude(r => r!.Log)
            .FirstOrDefaultAsync(l => l.Token == token);

        if (link == null)
            return NotFound(new SharedReportResponse { Success = false, Error = "Link not found" });

        if (!link.IsActive)
            return BadRequest(new SharedReportResponse { Success = false, Error = "Link is no longer active" });

        if (link.ExpiresAt.HasValue && link.ExpiresAt.Value < DateTime.UtcNow)
            return BadRequest(new SharedReportResponse { Success = false, Error = "Link has expired" });

        if (link.MaxAccesses.HasValue && link.AccessCount >= link.MaxAccesses.Value)
            return BadRequest(new SharedReportResponse { Success = false, Error = "Maximum access limit reached" });

        if (link.PasswordHash != null)
        {
            if (string.IsNullOrWhiteSpace(password))
                return Ok(new SharedReportResponse { Success = false, RequiresPassword = true });

            if (!BCrypt.Net.BCrypt.Verify(password, link.PasswordHash))
                return BadRequest(new SharedReportResponse { Success = false, Error = "Invalid password" });
        }

        // Read report content
        if (!System.IO.File.Exists(link.Report.ReportPath))
            return NotFound(new SharedReportResponse { Success = false, Error = "Report content not found" });

        var content = await System.IO.File.ReadAllTextAsync(link.Report.ReportPath);

        string? chartData = null;
        if (!string.IsNullOrWhiteSpace(link.Report.ChartPath) &&
            System.IO.File.Exists(link.Report.ChartPath))
        {
            chartData = await System.IO.File.ReadAllTextAsync(link.Report.ChartPath);
        }

        // Update access count
        link.AccessCount++;
        link.LastAccessedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new SharedReportResponse
        {
            Success = true,
            Report = new ReportDetail
            {
                Id = link.Report.Id,
                Title = link.Report.Title,
                Content = content,
                ChartData = chartData,
                Model = link.Report.Model,
                FileName = link.Report.Log?.FileName,
                CreatedAtUtc = link.Report.CreatedAtUtc,
                Status = link.Report.Status
            }
        });
    }

    /// <summary>
    /// Download shared report as PDF
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{token}/download")]
    public async Task<IActionResult> DownloadSharedReport(string token, [FromQuery] string? password = null)
    {
        var link = await _db.ShareableLinks
            .Include(l => l.Report)
            .FirstOrDefaultAsync(l => l.Token == token);

        if (link == null || !link.IsActive)
            return NotFound();

        if (link.ExpiresAt.HasValue && link.ExpiresAt.Value < DateTime.UtcNow)
            return BadRequest(new { error = "Link has expired" });

        if (link.MaxAccesses.HasValue && link.AccessCount >= link.MaxAccesses.Value)
            return BadRequest(new { error = "Maximum access limit reached" });

        if (link.PasswordHash != null && !string.IsNullOrWhiteSpace(password))
        {
            if (!BCrypt.Net.BCrypt.Verify(password, link.PasswordHash))
                return BadRequest(new { error = "Invalid password" });
        }
        else if (link.PasswordHash != null)
        {
            return BadRequest(new { error = "Password required" });
        }

        if (!System.IO.File.Exists(link.Report.ReportPath))
            return NotFound();

        var markdown = await System.IO.File.ReadAllTextAsync(link.Report.ReportPath);

        string? chartData = null;
        if (!string.IsNullOrWhiteSpace(link.Report.ChartPath) &&
            System.IO.File.Exists(link.Report.ChartPath))
        {
            chartData = await System.IO.File.ReadAllTextAsync(link.Report.ChartPath);
        }

        var pdfBytes = await PdfGenerator.FromMarkdownAsync(markdown, chartData);

        // Update access count
        link.AccessCount++;
        link.LastAccessedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var fileName = $"shared-report-{link.Report.Id}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    private static string GenerateToken()
    {
        var bytes = new byte[24];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}
