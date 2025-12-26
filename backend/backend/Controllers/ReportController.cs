using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Azure.Storage.Blobs;
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
    private readonly BlobServiceClient _blobService;
    private readonly IConfiguration _config;
    private readonly IHubNotificationService _hubNotification;

    public ReportsController(
        AppDbContext db,
        BlobServiceClient blobService,
        IConfiguration config,
        IHubNotificationService hubNotification)
    {
        _db = db;
        _blobService = blobService;
        _config = config;
        _hubNotification = hubNotification;
    }

    // ----------------------------------------------------
    // GET /api/reports
    // List reports (table view)
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
    // Report detail (modal viewer)
    // ----------------------------------------------------
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReportDetail>> Get(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");

        var report = await _db.Reports
            .Include(r => r.Log)
            .FirstOrDefaultAsync(r =>
                r.Id == id &&
                r.UserId == userId);

        if (report == null)
            return NotFound();

        var containerName = _config["AzureBlob:Container"]
            ?? throw new InvalidOperationException("AzureBlob:Container missing");

        var container = _blobService.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(report.ReportPath);

        if (!await blob.ExistsAsync())
            return NotFound("Report content missing");

        string content;
        await using (var stream = await blob.OpenReadAsync())
        using (var reader = new StreamReader(stream))
        {
            content = await reader.ReadToEndAsync();
        }

        return new ReportDetail
        {
            Id = report.Id,
            Title = report.Title,
            Content = content,
            Status = report.Status
        };
    }

    // ----------------------------------------------------
    // GET /api/reports/{id}/download
    // Download report TEXT
    // ----------------------------------------------------
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");

        var report = await _db.Reports
            .Include(r => r.Log)
            .FirstOrDefaultAsync(r =>
                r.Id == id &&
                r.UserId == userId);

        if (report == null)
            return NotFound();

        var containerName = _config["AzureBlob:Container"]
            ?? throw new InvalidOperationException("AzureBlob:Container missing");

        var container = _blobService.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(report.ReportPath);

        if (!await blob.ExistsAsync())
            return NotFound("Report file not found");

        var stream = await blob.OpenReadAsync();
        var fileName = Path.GetFileName(report.ReportPath);

        return File(stream, "text/plain", fileName);
    }

    // ----------------------------------------------------
    // DELETE /api/reports/{id}
    // Delete a single report
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

        // Delete from blob storage
        try
        {
            var containerName = _config["AzureBlob:Container"]
                ?? throw new InvalidOperationException("AzureBlob:Container missing");

            var container = _blobService.GetBlobContainerClient(containerName);
            var blob = container.GetBlobClient(report.ReportPath);

            await blob.DeleteIfExistsAsync();
        }
        catch (Exception ex)
        {
            // Log but don't fail - we still want to delete the DB record
            Console.WriteLine($"Failed to delete blob for report {id}: {ex.Message}");
        }

        // Delete from database
        _db.Reports.Remove(report);
        await _db.SaveChangesAsync();

        // Notify via SignalR
        await _hubNotification.NotifyReportDeletedAsync(userId, id);

        return NoContent();
    }

    // ----------------------------------------------------
    // DELETE /api/reports/all
    // Delete all reports for the current user
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

        var containerName = _config["AzureBlob:Container"]
            ?? throw new InvalidOperationException("AzureBlob:Container missing");

        var container = _blobService.GetBlobContainerClient(containerName);

        // Delete all blobs
        foreach (var report in reports)
        {
            try
            {
                var blob = container.GetBlobClient(report.ReportPath);
                await blob.DeleteIfExistsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete blob for report {report.Id}: {ex.Message}");
            }
        }

        // Delete all from database
        _db.Reports.RemoveRange(reports);
        await _db.SaveChangesAsync();

        // Notify via SignalR
        await _hubNotification.NotifyAllReportsDeletedAsync(userId, reports.Count);

        return Ok(new DeleteAllResponse { Deleted = reports.Count });
    }
}
