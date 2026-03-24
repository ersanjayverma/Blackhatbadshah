using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Common;
using backend.Data;
using backend.Services;
using shared.Dto;

namespace backend.Controllers;

[Route("api/reports")]
[Authorize]
public class ReportsController : BaseApiController
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IHubNotificationService _hubNotification;
    private readonly IQdrantService _qdrantService;
    private readonly string _storageRoot;

    public ReportsController(
        AppDbContext db,
        IConfiguration config,
        IHubNotificationService hubNotification,
        IQdrantService qdrantService)
    {
        _db = db;
        _config = config;
        _hubNotification = hubNotification;
        _qdrantService = qdrantService;

        _storageRoot = _config["Storage:RootPath"]
            ?? throw new InvalidOperationException("Storage:RootPath not configured");

        Directory.CreateDirectory(_storageRoot);
        Directory.CreateDirectory(Path.Combine(_storageRoot, "reports"));
        Directory.CreateDirectory(Path.Combine(_storageRoot, "charts"));
    }

    /// <summary>
    /// Get all reports
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetUserId();

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

    /// <summary>
    /// Get a specific report
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReportDetail>> Get(Guid id)
    {
        var userId = GetUserId();

        try
        {
            var report = await _db.Reports
                .Include(r => r.Log)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (report == null)
                return NotFound();

            if (!System.IO.File.Exists(report.ReportPath))
                return NotFound(ErrorMessages.ReportContentMissing);

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
            Console.WriteLine($"Error fetching report {id}: {ex.Message}");
            return StatusCode(500, new { error = "Failed to fetch report", details = ex.Message });
        }
    }

    /// <summary>
    /// Download report as PDF
    /// </summary>
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        try
        {
            if (!TryGetUserId(out var userId))
                return UnauthorizedWithError(ErrorMessages.Unauthorized);

            var report = await _db.Reports
                .Include(r => r.Log)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (report == null)
                return NotFoundWithError(ErrorMessages.ReportNotFound);

            if (string.IsNullOrEmpty(report.ReportPath) || !System.IO.File.Exists(report.ReportPath))
                return NotFoundWithError(ErrorMessages.ReportFileNotFound);

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

    /// <summary>
    /// Get similar reports based on vector similarity search
    /// </summary>
    [HttpGet("{id:guid}/similar")]
    public async Task<IActionResult> GetSimilar(Guid id, [FromQuery] int? limit = 5)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var report = await _db.Reports
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

        if (report == null)
            return NotFound();

        if (string.IsNullOrEmpty(report.ReportPath) || !System.IO.File.Exists(report.ReportPath))
            return NotFound(ErrorMessages.ReportContentMissing);

        var content = await System.IO.File.ReadAllTextAsync(report.ReportPath);

        var similarResults = await _qdrantService.SearchSimilarAsync(
            userId,
            content,
            systemId: null,
            limit: (limit ?? 5) + 1);

        var filteredResults = similarResults
            .Where(r => r.ReportId != id)
            .Take(limit ?? 5)
            .ToList();

        return Ok(new
        {
            reportId = id,
            similarReports = filteredResults
        });
    }

    /// <summary>
    /// Delete a report
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            if (!TryGetUserId(out var userId))
                return UnauthorizedWithError(ErrorMessages.Unauthorized);

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

            await _qdrantService.DeleteAnalysisAsync(id);
            await _hubNotification.NotifyReportDeletedAsync(userId, id);

            return NoContent();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting report {id}: {ex.Message}");
            return StatusCode(500, new { error = "Failed to delete report", details = ex.Message });
        }
    }

    /// <summary>
    /// Export report as JSON
    /// </summary>
    [HttpGet("{id:guid}/export/json")]
    public async Task<IActionResult> ExportJson(Guid id)
    {
        try
        {
            if (!TryGetUserId(out var userId))
                return UnauthorizedWithError(ErrorMessages.Unauthorized);

            var report = await _db.Reports
                .Include(r => r.Log)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (report == null)
                return NotFoundWithError(ErrorMessages.ReportNotFound);

            if (string.IsNullOrEmpty(report.ReportPath) || !System.IO.File.Exists(report.ReportPath))
                return NotFoundWithError(ErrorMessages.ReportFileNotFound);

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

    /// <summary>
    /// Export report as CSV
    /// </summary>
    [HttpGet("{id:guid}/export/csv")]
    public async Task<IActionResult> ExportCsv(Guid id)
    {
        try
        {
            if (!TryGetUserId(out var userId))
                return UnauthorizedWithError(ErrorMessages.Unauthorized);

            var report = await _db.Reports
                .Include(r => r.Log)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (report == null)
                return NotFoundWithError(ErrorMessages.ReportNotFound);

            if (string.IsNullOrEmpty(report.ReportPath) || !System.IO.File.Exists(report.ReportPath))
                return NotFoundWithError(ErrorMessages.ReportFileNotFound);

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

    /// <summary>
    /// Export report as Markdown
    /// </summary>
    [HttpGet("{id:guid}/export/markdown")]
    public async Task<IActionResult> ExportMarkdown(Guid id)
    {
        try
        {
            if (!TryGetUserId(out var userId))
                return UnauthorizedWithError(ErrorMessages.Unauthorized);

            var report = await _db.Reports
                .Include(r => r.Log)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (report == null)
                return NotFoundWithError(ErrorMessages.ReportNotFound);

            if (string.IsNullOrEmpty(report.ReportPath) || !System.IO.File.Exists(report.ReportPath))
                return NotFoundWithError(ErrorMessages.ReportFileNotFound);

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

    /// <summary>
    /// Bulk export reports
    /// </summary>
    [HttpPost("bulk-export")]
    public async Task<IActionResult> BulkExport([FromBody] BulkExportRequest request)
    {
        try
        {
            if (!TryGetUserId(out var userId))
                return UnauthorizedWithError(ErrorMessages.Unauthorized);

            if (request.ReportIds == null || request.ReportIds.Count == 0)
                return BadRequestWithError(ErrorMessages.NoReportIdsProvided);

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

    /// <summary>
    /// Delete all reports
    /// </summary>
    [HttpDelete("all")]
    public async Task<IActionResult> DeleteAll()
    {
        try
        {
            if (!TryGetUserId(out var userId))
                return UnauthorizedWithError(ErrorMessages.Unauthorized);

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

            await _qdrantService.DeleteUserDataAsync(userId);
            await _hubNotification.NotifyAllReportsDeletedAsync(userId, reports.Count);

            return Ok(new DeleteAllResponse { Deleted = reports.Count });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting all reports: {ex.Message}");
            return StatusCode(500, new { error = "Failed to delete reports", details = ex.Message });
        }
    }

    private static string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        return field.Replace("\"", "\"\"").Replace("\r\n", " ").Replace("\n", " ");
    }
}
