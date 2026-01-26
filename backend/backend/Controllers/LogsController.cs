using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using UglyToad.PdfPig;

using backend.Data;
using backend.Data.Entities;
using backend.Services;
using shared.Dto;

namespace backend.Controllers;

[ApiController]
[Route("api/logs")]
[Authorize]
public class LogsController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly ILogAnalyzer _analyzer;
    private readonly ITextractService _textractService;
    private readonly IHubNotificationService _hubNotification;
    private readonly ILogAnalysisQueue _analysisQueue;
    private readonly IPlanEnforcementService _planEnforcement;
    private readonly string _storageRoot;

    // ----------------------------
    // Supported formats
    // ----------------------------
    private static readonly HashSet<string> TextExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        { ".txt", ".log", ".json", ".xml", ".csv" };

    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".tiff" };

    private static readonly HashSet<string> PdfExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        { ".pdf" };

    public LogsController(
        IConfiguration config,
        AppDbContext db,
        ILogAnalyzer analyzer,
        ITextractService textractService,
        IHubNotificationService hubNotification,
        ILogAnalysisQueue analysisQueue,
        IPlanEnforcementService planEnforcement)
    {
        _config = config;
        _db = db;
        _analyzer = analyzer;
        _textractService = textractService;
        _hubNotification = hubNotification;
        _analysisQueue = analysisQueue;
        _planEnforcement = planEnforcement;

        _storageRoot = _config["Storage:RootPath"]
            ?? throw new InvalidOperationException("Storage:RootPath not configured");

        Directory.CreateDirectory(_storageRoot);
        Directory.CreateDirectory(Path.Combine(_storageRoot, "logs"));
        Directory.CreateDirectory(Path.Combine(_storageRoot, "reports"));
    }

    // -----------------------------------------
    // POST api/logs/upload
    // -----------------------------------------
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("File is required.");

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest("File size exceeds 10 MB.");

        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        if (userId == null)
            return Unauthorized();

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext))
            return BadRequest("File extension missing.");

        string extractedText;

        if (TextExtensions.Contains(ext))
        {
            using var reader = new StreamReader(file.OpenReadStream());
            extractedText = await reader.ReadToEndAsync();
        }
        else if (PdfExtensions.Contains(ext))
        {
            var pdfText = await TryExtractPdfTextAsync(file);
            extractedText = !string.IsNullOrWhiteSpace(pdfText)
                ? pdfText
                : await _textractService.ExtractTextAsync(file);
        }
        else if (ImageExtensions.Contains(ext))
        {
            extractedText = await _textractService.ExtractTextAsync(file);
        }
        else
        {
            return BadRequest($"Unsupported file type '{ext}'.");
        }

        if (string.IsNullOrWhiteSpace(extractedText))
            return BadRequest("No extractable text found.");

        var logId = Guid.NewGuid();
        var filePath = Path.Combine(_storageRoot, "logs", $"{logId}.txt");

        await System.IO.File.WriteAllTextAsync(filePath, extractedText, Encoding.UTF8);

        var entity = new Log
        {
            Id = logId,
            UserId = userId,
            FileName = Path.GetFileName(file.FileName),
            ContentType = "text/plain",
            SizeBytes = extractedText.Length,
            StoragePath = filePath,
            CreatedAt = DateTime.UtcNow
        };

        _db.Logs.Add(entity);
        await _db.SaveChangesAsync();

        await _hubNotification.NotifyLogCreatedAsync(userId, logId, entity.FileName);

        return Ok(new { logId });
    }

    // -----------------------------------------
    // GET api/logs/{id}
    // -----------------------------------------
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        var log = await _db.Logs.FindAsync(id);
        if (log == null) return NotFound();
        if (log.UserId != userId) return Forbid();

        return Ok(new LogDto
        {
            Id = log.Id,
            FileName = log.FileName,
            SizeBytes = log.SizeBytes,
            CreatedAt = log.CreatedAt
        });
    }

    // -----------------------------------------
    // GET api/logs/{id}/content
    // -----------------------------------------
    [HttpGet("{id:guid}/content")]
    public IActionResult GetContent(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        var log = _db.Logs.Find(id);
        if (log == null) return NotFound();
        if (log.UserId != userId) return Forbid();

        if (!System.IO.File.Exists(log.StoragePath))
            return NotFound("File missing");

        return PhysicalFile(log.StoragePath, "text/plain");
    }

    // -----------------------------------------
    // GET api/logs
    // -----------------------------------------
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        var logs = await _db.Logs.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .Select(x => new LogDto
            {
                Id = x.Id,
                FileName = x.FileName,
                SizeBytes = x.SizeBytes,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        return Ok(logs);
    }

    // -----------------------------------------
    // DELETE api/logs/{id}
    // -----------------------------------------
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        var log = await _db.Logs.FindAsync(id);
        if (log == null) return NotFound();
        if (log.UserId != userId) return Forbid();

        try
        {
            if (System.IO.File.Exists(log.StoragePath))
                System.IO.File.Delete(log.StoragePath);
        }
        catch { }

        _db.Logs.Remove(log);
        await _db.SaveChangesAsync();

        await _hubNotification.NotifyLogDeletedAsync(userId, id);

        return NoContent();
    }

    // -----------------------------------------
    // DELETE api/logs/bulk
    // -----------------------------------------
    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");
        if (userId == null) return Unauthorized();

        if (request.Ids == null || request.Ids.Count == 0)
            return BadRequest(new { error = "No log IDs provided" });

        var successCount = 0;
        var errors = new List<BulkOperationError>();

        foreach (var id in request.Ids)
        {
            try
            {
                var log = await _db.Logs.FindAsync(id);
                if (log == null)
                {
                    errors.Add(new BulkOperationError { Id = id, Error = "Log not found" });
                    continue;
                }
                if (log.UserId != userId)
                {
                    errors.Add(new BulkOperationError { Id = id, Error = "Access denied" });
                    continue;
                }

                try
                {
                    if (System.IO.File.Exists(log.StoragePath))
                        System.IO.File.Delete(log.StoragePath);
                }
                catch { }

                _db.Logs.Remove(log);
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add(new BulkOperationError { Id = id, Error = ex.Message });
            }
        }

        await _db.SaveChangesAsync();

        // Notify frontend
        foreach (var id in request.Ids.Where(id => !errors.Any(e => e.Id == id)))
        {
            await _hubNotification.NotifyLogDeletedAsync(userId, id);
        }

        return Ok(new BulkOperationResponse
        {
            TotalRequested = request.Ids.Count,
            SuccessCount = successCount,
            FailedCount = errors.Count,
            Errors = errors.Count > 0 ? errors : null
        });
    }

    // -----------------------------------------
    // POST api/logs/bulk-analyze
    // -----------------------------------------
    [HttpPost("bulk-analyze")]
    public async Task<IActionResult> BulkAnalyze([FromBody] BulkAnalyzeRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");
        if (userId == null) return Unauthorized();

        if (request.LogIds == null || request.LogIds.Count == 0)
            return BadRequest(new { error = "No log IDs provided" });

        // Check plan limits
        var (allowed, message) = await _planEnforcement.CheckAnalysisAllowedAsync(userId, request.Model);
        if (!allowed)
        {
            return StatusCode(402, new { error = message, upgradeRequired = true });
        }

        var authHeader = Request.Headers.Authorization.ToString();
        var token = authHeader.StartsWith("Bearer ") ? authHeader[7..] : null;
        if (token == null) return Unauthorized();

        var successCount = 0;
        var errors = new List<BulkOperationError>();

        foreach (var id in request.LogIds)
        {
            try
            {
                var log = await _db.Logs.FindAsync(id);
                if (log == null)
                {
                    errors.Add(new BulkOperationError { Id = id, Error = "Log not found" });
                    continue;
                }
                if (log.UserId != userId)
                {
                    errors.Add(new BulkOperationError { Id = id, Error = "Access denied" });
                    continue;
                }

                await _analysisQueue.QueueAnalysisJobAsync(id, userId, token, request.Model);
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add(new BulkOperationError { Id = id, Error = ex.Message });
            }
        }

        return Accepted(new BulkOperationResponse
        {
            TotalRequested = request.LogIds.Count,
            SuccessCount = successCount,
            FailedCount = errors.Count,
            Errors = errors.Count > 0 ? errors : null
        });
    }

    // -----------------------------------------
    // DELETE api/logs/all
    // -----------------------------------------
    [HttpDelete("all")]
    public async Task<IActionResult> DeleteAll()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");
        if (userId == null) return Unauthorized();

        var logs = await _db.Logs.Where(l => l.UserId == userId).ToListAsync();

        foreach (var log in logs)
        {
            try
            {
                if (System.IO.File.Exists(log.StoragePath))
                    System.IO.File.Delete(log.StoragePath);
            }
            catch { }
        }

        _db.Logs.RemoveRange(logs);
        await _db.SaveChangesAsync();

        await _hubNotification.NotifyAllLogsDeletedAsync(userId, logs.Count);

        return Ok(new DeleteAllResponse { Deleted = logs.Count });
    }

    // -----------------------------------------
    // POST api/logs/{id}/analyze
    // -----------------------------------------
    [HttpPost("{id:guid}/analyze")]
    public async Task<IActionResult> QueueAnalysis(Guid id, [FromBody] AnalyzeLogRequest? request = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        var log = _db.Logs.Find(id);
        if (log == null) return NotFound();
        if (log.UserId != userId) return Forbid();

        // Check plan limits and model access
        var (allowed, message) = await _planEnforcement.CheckAnalysisAllowedAsync(userId, request?.Model);
        if (!allowed)
        {
            return StatusCode(402, new { error = message, upgradeRequired = true });
        }

        var authHeader = Request.Headers.Authorization.ToString();
        var token = authHeader.StartsWith("Bearer ") ? authHeader[7..] : null;
        if (token == null) return Unauthorized();

        // Usage is recorded in LogAnalysisBackgroundWorker only after successful analysis

        await _analysisQueue.QueueAnalysisJobAsync(id, userId, token, request?.Model);
        return Accepted(new { message = "Analysis queued", logId = id });
    }

    // -----------------------------------------
    // GET api/logs/{id}/analyze (legacy)
    // -----------------------------------------
    [HttpGet("{id:guid}/analyze")]
    public async Task<IActionResult> AnalyseContent(Guid id, [FromQuery] string? model = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        var log = await _db.Logs.FindAsync(id);
        if (log == null) return NotFound();
        if (log.UserId != userId) return Forbid();

        if (!System.IO.File.Exists(log.StoragePath))
            return NotFound("Log file missing");

        var logContent = await System.IO.File.ReadAllTextAsync(log.StoragePath);

        var analysis = await _analyzer.AnalyzeAsync(log.Id, logContent, model);
        var analysisText = analysis.reply ?? analysis.ToString()!;

        var reportId = Guid.NewGuid();
        var reportDir = Path.Combine(_storageRoot, "reports", log.Id.ToString());
        Directory.CreateDirectory(reportDir);

        var reportPath = Path.Combine(reportDir, $"{reportId}.txt");
        await System.IO.File.WriteAllTextAsync(reportPath, analysisText, Encoding.UTF8);

        var report = new Report
        {
            Id = reportId,
            LogId = log.Id,
            UserId = userId,
            Title = $"Analysis Report – {log.FileName}",
            Summary = analysisText.Length > 500 ? analysisText[..500] : analysisText,
            ReportPath = reportPath,
            Status = ReportStatus.Completed,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Reports.Add(report);
        await _db.SaveChangesAsync();

        await _hubNotification.NotifyReportCreatedAsync(userId, new ReportListItem
        {
            Id = report.Id,
            Title = report.Title,
            FileName = log.FileName,
            CreatedAtUtc = report.CreatedAtUtc,
            Status = ReportStatus.Completed
        });

        return Ok(analysis);
    }

    // -----------------------------------------
    // PDF text probe
    // -----------------------------------------
    private static async Task<string?> TryExtractPdfTextAsync(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        using var pdf = PdfDocument.Open(stream);

        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);

        var text = sb.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
