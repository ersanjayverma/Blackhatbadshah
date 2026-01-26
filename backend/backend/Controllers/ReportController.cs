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

        try
        {
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
        catch (Exception ex)
        {
            // Log the error for debugging
            Console.WriteLine($"Error fetching report {id}: {ex.Message}");
            return StatusCode(500, new { error = "Failed to fetch report", details = ex.Message });
        }
    }

    // ----------------------------------------------------
    // GET /api/reports/{id}/download
    // ----------------------------------------------------
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "Unable to determine user identity" });

            var report = await _db.Reports
                .Include(r => r.Log)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (report == null)
                return NotFound(new { error = "Report not found" });

            if (string.IsNullOrEmpty(report.ReportPath) || !System.IO.File.Exists(report.ReportPath))
                return NotFound(new { error = "Report file not found on disk" });

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
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading report {id}: {ex.Message}");
            return StatusCode(500, new { error = "Failed to download report", details = ex.Message });
        }
    }


    // ----------------------------------------------------
    // DELETE /api/reports/{id}
    // ----------------------------------------------------
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("sub")
                         ?? string.Empty;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "Unable to determine user identity" });

            var report = await _db.Reports
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (report == null)
                return NotFound();

            try
            {
                if (!string.IsNullOrEmpty(report.ReportPath) && System.IO.File.Exists(report.ReportPath))
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
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting report {id}: {ex.Message}");
            return StatusCode(500, new { error = "Failed to delete report", details = ex.Message });
        }
    }

    // ----------------------------------------------------
    // GET /api/reports/{id}/export/json
    // ----------------------------------------------------
    [HttpGet("{id:guid}/export/json")]
    public async Task<IActionResult> ExportJson(Guid id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "Unable to determine user identity" });

            var report = await _db.Reports
                .Include(r => r.Log)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (report == null)
                return NotFound(new { error = "Report not found" });

            if (string.IsNullOrEmpty(report.ReportPath) || !System.IO.File.Exists(report.ReportPath))
                return NotFound(new { error = "Report file not found on disk" });

            var content = await System.IO.File.ReadAllTextAsync(report.ReportPath);

            Dictionary<string, object>? chartData = null;
            if (!string.IsNullOrWhiteSpace(report.ChartPath) && System.IO.File.Exists(report.ChartPath))
            {
                var chartJson = await System.IO.File.ReadAllTextAsync(report.ChartPath);
                try
                {
                    chartData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(chartJson);
                }
                catch { }
            }

            var exportData = new ReportExportData
            {
                ReportId = report.Id,
                Title = report.Title,
                FileName = report.Log?.FileName,
                Model = report.Model,
                Status = report.Status.ToString(),
                CreatedAt = report.CreatedAtUtc,
                Content = content,
                ChartData = chartData,
                Metadata = new Dictionary<string, string>
                {
                    ["ExportedAt"] = DateTime.UtcNow.ToString("O"),
                    ["ExportFormat"] = "JSON"
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            var fileName = $"report-{report.Id}.json";
            return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", fileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting report {id} to JSON: {ex.Message}");
            return StatusCode(500, new { error = "Failed to export report", details = ex.Message });
        }
    }

    // ----------------------------------------------------
    // GET /api/reports/{id}/export/csv
    // ----------------------------------------------------
    [HttpGet("{id:guid}/export/csv")]
    public async Task<IActionResult> ExportCsv(Guid id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "Unable to determine user identity" });

            var report = await _db.Reports
                .Include(r => r.Log)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (report == null)
                return NotFound(new { error = "Report not found" });

            if (string.IsNullOrEmpty(report.ReportPath) || !System.IO.File.Exists(report.ReportPath))
                return NotFound(new { error = "Report file not found on disk" });

            var content = await System.IO.File.ReadAllTextAsync(report.ReportPath);

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Field,Value");
            csv.AppendLine($"ReportId,\"{report.Id}\"");
            csv.AppendLine($"Title,\"{EscapeCsvField(report.Title)}\"");
            csv.AppendLine($"FileName,\"{EscapeCsvField(report.Log?.FileName ?? "N/A")}\"");
            csv.AppendLine($"Model,\"{EscapeCsvField(report.Model ?? "N/A")}\"");
            csv.AppendLine($"Status,\"{report.Status}\"");
            csv.AppendLine($"CreatedAt,\"{report.CreatedAtUtc:O}\"");
            csv.AppendLine($"Content,\"{EscapeCsvField(content)}\"");

            var fileName = $"report-{report.Id}.csv";
            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting report {id} to CSV: {ex.Message}");
            return StatusCode(500, new { error = "Failed to export report", details = ex.Message });
        }
    }

    // ----------------------------------------------------
    // GET /api/reports/{id}/export/markdown
    // ----------------------------------------------------
    [HttpGet("{id:guid}/export/markdown")]
    public async Task<IActionResult> ExportMarkdown(Guid id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "Unable to determine user identity" });

            var report = await _db.Reports
                .Include(r => r.Log)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (report == null)
                return NotFound(new { error = "Report not found" });

            if (string.IsNullOrEmpty(report.ReportPath) || !System.IO.File.Exists(report.ReportPath))
                return NotFound(new { error = "Report file not found on disk" });

            var content = await System.IO.File.ReadAllTextAsync(report.ReportPath);

            var markdown = new System.Text.StringBuilder();
            markdown.AppendLine($"# {report.Title}");
            markdown.AppendLine();
            markdown.AppendLine("## Report Metadata");
            markdown.AppendLine();
            markdown.AppendLine($"- **Report ID:** {report.Id}");
            markdown.AppendLine($"- **File:** {report.Log?.FileName ?? "N/A"}");
            markdown.AppendLine($"- **Model:** {report.Model ?? "N/A"}");
            markdown.AppendLine($"- **Status:** {report.Status}");
            markdown.AppendLine($"- **Created:** {report.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
            markdown.AppendLine();
            markdown.AppendLine("## Analysis Content");
            markdown.AppendLine();
            markdown.AppendLine(content);

            var fileName = $"report-{report.Id}.md";
            return File(System.Text.Encoding.UTF8.GetBytes(markdown.ToString()), "text/markdown", fileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting report {id} to Markdown: {ex.Message}");
            return StatusCode(500, new { error = "Failed to export report", details = ex.Message });
        }
    }

    // ----------------------------------------------------
    // POST /api/reports/bulk-export
    // ----------------------------------------------------
    [HttpPost("bulk-export")]
    public async Task<IActionResult> BulkExport([FromBody] BulkExportRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "Unable to determine user identity" });

            if (request.ReportIds == null || request.ReportIds.Count == 0)
                return BadRequest(new { error = "No report IDs provided" });

            var exportList = new List<ReportExportData>();

            foreach (var reportId in request.ReportIds)
            {
                var report = await _db.Reports
                    .Include(r => r.Log)
                    .FirstOrDefaultAsync(r => r.Id == reportId && r.UserId == userId);

                if (report == null) continue;
                if (string.IsNullOrEmpty(report.ReportPath) || !System.IO.File.Exists(report.ReportPath)) continue;

                var content = await System.IO.File.ReadAllTextAsync(report.ReportPath);

                exportList.Add(new ReportExportData
                {
                    ReportId = report.Id,
                    Title = report.Title,
                    FileName = report.Log?.FileName,
                    Model = report.Model,
                    Status = report.Status.ToString(),
                    CreatedAt = report.CreatedAtUtc,
                    Content = content
                });
            }

            var json = System.Text.Json.JsonSerializer.Serialize(exportList, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            var fileName = $"reports-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", fileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error bulk exporting reports: {ex.Message}");
            return StatusCode(500, new { error = "Failed to export reports", details = ex.Message });
        }
    }

    private static string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        return field.Replace("\"", "\"\"").Replace("\r\n", " ").Replace("\n", " ");
    }

    // ----------------------------------------------------
    // DELETE /api/reports/all
    // ----------------------------------------------------
    [HttpDelete("all")]
    public async Task<IActionResult> DeleteAll()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("sub")
                         ?? string.Empty;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "Unable to determine user identity" });

            var reports = await _db.Reports
                .Where(r => r.UserId == userId)
                .ToListAsync();

            if (reports.Count == 0)
                return Ok(new DeleteAllResponse { Deleted = 0 });

            foreach (var report in reports)
            {
                try
                {
                    if (!string.IsNullOrEmpty(report.ReportPath) && System.IO.File.Exists(report.ReportPath))
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
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting all reports: {ex.Message}");
            return StatusCode(500, new { error = "Failed to delete reports", details = ex.Message });
        }
    }
}
