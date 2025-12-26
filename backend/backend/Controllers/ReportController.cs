using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Azure.Storage.Blobs;
using backend.Data;
using backend.Data.Entities;
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

    public ReportsController(
        AppDbContext db,
        BlobServiceClient blobService,
        IConfiguration config)
    {
        _db = db;
        _blobService = blobService;
        _config = config;
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
            .Where(r => r.Log.UserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new
            {
                r.Id,
                r.Title,
                FileName = r.Log.FileName,
                r.CreatedAtUtc
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
                r.Log.UserId == userId);

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
            Content = content
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
                r.Log.UserId == userId);

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
}
