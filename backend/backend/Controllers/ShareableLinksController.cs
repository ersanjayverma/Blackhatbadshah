using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using backend.Common;
using backend.Data;
using backend.Data.Entities;
using backend.Services;
using shared.Dto;

namespace backend.Controllers;

[Route("api/share")]
public class ShareableLinksController : BaseApiController
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

    private string GetBaseUrl() => $"{Request.Scheme}://{Request.Host}";

    /// <summary>
    /// Get all shareable links for the current user
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetLinks()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var baseUrl = GetBaseUrl();

        var links = await _db.ShareableLinks
            .Include(l => l.Report)
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => l.ToDto(baseUrl))
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
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var report = await _db.Reports.FirstOrDefaultAsync(r => r.Id == request.ReportId && r.UserId == userId);
        if (report == null)
            return NotFoundWithError(ErrorMessages.ReportNotFound);

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

        return Ok(link.ToDto(GetBaseUrl(), report.Title));
    }

    /// <summary>
    /// Delete a shareable link
    /// </summary>
    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLink(Guid id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var link = await _db.ShareableLinks.FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId);
        if (link == null)
            return NotFound();

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
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var link = await _db.ShareableLinks.FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId);
        if (link == null)
            return NotFound();

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
            return NotFound(new SharedReportResponse { Success = false, Error = ErrorMessages.LinkNotFound });

        if (!link.IsActive)
            return BadRequest(new SharedReportResponse { Success = false, Error = ErrorMessages.LinkInactive });

        if (link.ExpiresAt.HasValue && link.ExpiresAt.Value < DateTime.UtcNow)
            return BadRequest(new SharedReportResponse { Success = false, Error = ErrorMessages.LinkExpired });

        if (link.MaxAccesses.HasValue && link.AccessCount >= link.MaxAccesses.Value)
            return BadRequest(new SharedReportResponse { Success = false, Error = ErrorMessages.MaxAccessLimitReached });

        if (link.PasswordHash != null)
        {
            if (string.IsNullOrWhiteSpace(password))
                return Ok(new SharedReportResponse { Success = false, RequiresPassword = true });

            if (!BCrypt.Net.BCrypt.Verify(password, link.PasswordHash))
                return BadRequest(new SharedReportResponse { Success = false, Error = ErrorMessages.InvalidPassword });
        }

        if (!System.IO.File.Exists(link.Report.ReportPath))
            return NotFound(new SharedReportResponse { Success = false, Error = ErrorMessages.ReportContentMissing });

        var content = await System.IO.File.ReadAllTextAsync(link.Report.ReportPath);

        string? chartData = null;
        if (!string.IsNullOrWhiteSpace(link.Report.ChartPath) &&
            System.IO.File.Exists(link.Report.ChartPath))
        {
            chartData = await System.IO.File.ReadAllTextAsync(link.Report.ChartPath);
        }

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
            return BadRequestWithError(ErrorMessages.LinkExpired);

        if (link.MaxAccesses.HasValue && link.AccessCount >= link.MaxAccesses.Value)
            return BadRequestWithError(ErrorMessages.MaxAccessLimitReached);

        if (link.PasswordHash != null)
        {
            if (string.IsNullOrWhiteSpace(password))
                return BadRequestWithError(ErrorMessages.PasswordRequired);

            if (!BCrypt.Net.BCrypt.Verify(password, link.PasswordHash))
                return BadRequestWithError(ErrorMessages.InvalidPassword);
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
