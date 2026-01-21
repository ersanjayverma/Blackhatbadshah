using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Data.Entities;
using backend.Services;
using shared.Dto;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IHubNotificationService _hubNotification;
    private readonly string _storageRoot;

    public ReportsController(
        AppDbContext db,
        IConfiguration config,
        IHubNotificationService hubNotification)
    {
        _db = db;
        _config = config;
        _hubNotification = hubNotification;

        _storageRoot = _config["Storage:RootPath"]
            ?? throw new InvalidOperationException("Storage:RootPath not configured");

        Directory.CreateDirectory(_storageRoot);
        Directory.CreateDirectory(Path.Combine(_storageRoot, "reports"));
        Directory.CreateDirectory(Path.Combine(_storageRoot, "charts"));
    }

    // ----------------------------------------------------
    // GET /api/reports
    // ----------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        var reports = await _db.Reports
            .Include(r => r.Log)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new ReportListItem
            {
                Id = r.Id,
                Title = r.Title,
                FileName = r.Log != null ? r.Log.FileName : "Log Deleted",
                CreatedAtUtc = r.CreatedAtUtc,
                Status = r.Status
            })
            .ToListAsync();

        return Ok(reports);
    }

    // ----------------------------------------------------
    // GET /api/reports/{id}
    // ----------------------------------------------------
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReportDetail>> Get(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        var report = await _db.Reports
            .Include(r => r.Log)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

        if (report == null)
            return NotFound();

        if (!System.IO.File.Exists(report.ReportPath))
            return NotFound("Report content missing");

        var content = await System.IO.File.ReadAllTextAsync(report.ReportPath);

        string? chartData = null;
        if (!string.IsNullOrWhiteSpace(report.ChartPath) &&
            System.IO.File.Exists(report.ChartPath))
        {
            chartData = await System.IO.File.ReadAllTextAsync(report.ChartPath);
        }

        return new ReportDetail
        {
            Id = report.Id,
            Title = report.Title,
            Content = content,
            ChartData = chartData,
            Model = report.Model,
            FileName = report.Log?.FileName ?? "Log Deleted",
            CreatedAtUtc = report.CreatedAtUtc,
            Status = report.Status
        };
    }

    // ----------------------------------------------------
    // GET /api/reports/{id}/download
    // ----------------------------------------------------
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        var report = await _db.Reports
            .Include(r => r.Log)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

        if (report == null)
            return NotFound();

        if (!System.IO.File.Exists(report.ReportPath))
            return NotFound("Report file not found");

        var markdown = await System.IO.File.ReadAllTextAsync(report.ReportPath);

        string? chartData = null;
        if (!string.IsNullOrWhiteSpace(report.ChartPath) &&
            System.IO.File.Exists(report.ChartPath))
        {
            chartData = await System.IO.File.ReadAllTextAsync(report.ChartPath);
        }

        var originalName = Path.GetFileNameWithoutExtension(report.ReportPath);
        var fileName = $"{originalName}.pdf";

        var pdfBytes = await PdfGenerator.FromMarkdownAsync(markdown, chartData);

        return File(pdfBytes, "application/pdf", fileName);
    }

    // ----------------------------------------------------
    // DELETE /api/reports/{id}
    // ----------------------------------------------------
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub")
                     ?? string.Empty;

        var report = await _db.Reports
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

        if (report == null)
            return NotFound();

        try
        {
            if (System.IO.File.Exists(report.ReportPath))
                System.IO.File.Delete(report.ReportPath);

            if (!string.IsNullOrWhiteSpace(report.ChartPath) &&
                System.IO.File.Exists(report.ChartPath))
                System.IO.File.Delete(report.ChartPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to delete files for report {id}: {ex.Message}");
        }

        _db.Reports.Remove(report);
        await _db.SaveChangesAsync();

        await _hubNotification.NotifyReportDeletedAsync(userId, id);

        return NoContent();
    }

    // ----------------------------------------------------
    // DELETE /api/reports/all
    // ----------------------------------------------------
    [HttpDelete("all")]
    public async Task<IActionResult> DeleteAll()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub")
                     ?? string.Empty;

        var reports = await _db.Reports
            .Where(r => r.UserId == userId)
            .ToListAsync();

        if (reports.Count == 0)
            return NoContent();

        foreach (var report in reports)
        {
            try
            {
                if (System.IO.File.Exists(report.ReportPath))
                    System.IO.File.Delete(report.ReportPath);

                if (!string.IsNullOrWhiteSpace(report.ChartPath) &&
                    System.IO.File.Exists(report.ChartPath))
                    System.IO.File.Delete(report.ChartPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete files for report {report.Id}: {ex.Message}");
            }
        }

        _db.Reports.RemoveRange(reports);
        await _db.SaveChangesAsync();

        await _hubNotification.NotifyAllReportsDeletedAsync(userId, reports.Count);

        return Ok(new DeleteAllResponse { Deleted = reports.Count });
    }
}
