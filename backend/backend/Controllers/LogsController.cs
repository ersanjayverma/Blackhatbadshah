using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using UglyToad.PdfPig;
using backend.Common;
using backend.Data;
using backend.Data.Entities;
using backend.Services;
using shared.Dto;

namespace backend.Controllers;

[Route("api/logs")]
[Authorize]
public class LogsController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly ILogAnalyzer _analyzer;
    private readonly ITextractService _textractService;
    private readonly IHubNotificationService _hubNotification;
    private readonly ILogAnalysisQueue _analysisQueue;
    private readonly IPlanEnforcementService _planEnforcement;
    private readonly ITagService _tagService;
    private readonly string _storageRoot;

    public LogsController(
        IConfiguration config,
        AppDbContext db,
        ILogAnalyzer analyzer,
        ITextractService textractService,
        IHubNotificationService hubNotification,
        ILogAnalysisQueue analysisQueue,
        IPlanEnforcementService planEnforcement,
        ITagService tagService)
    {
        _config = config;
        _db = db;
        _analyzer = analyzer;
        _textractService = textractService;
        _hubNotification = hubNotification;
        _analysisQueue = analysisQueue;
        _planEnforcement = planEnforcement;
        _tagService = tagService;

        _storageRoot = _config["Storage:RootPath"]
            ?? throw new InvalidOperationException("Storage:RootPath not configured");

        Directory.CreateDirectory(_storageRoot);
        Directory.CreateDirectory(Path.Combine(_storageRoot, "logs"));
        Directory.CreateDirectory(Path.Combine(_storageRoot, "reports"));
    }

    /// <summary>
    /// Upload a log file
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string? tagIds = null)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ErrorMessages.FileRequired);

        if (file.Length > Defaults.MaxFileSizeBytes)
            return BadRequest(ErrorMessages.FileTooLarge);

        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext))
            return BadRequest(ErrorMessages.FileExtensionMissing);

        string extractedText;

        if (FileExtensions.IsText(ext))
        {
            using var reader = new StreamReader(file.OpenReadStream());
            extractedText = await reader.ReadToEndAsync();
        }
        else if (FileExtensions.IsPdf(ext))
        {
            var pdfText = await TryExtractPdfTextAsync(file);
            extractedText = !string.IsNullOrWhiteSpace(pdfText)
                ? pdfText
                : await _textractService.ExtractTextAsync(file);
        }
        else if (FileExtensions.IsImage(ext))
        {
            extractedText = await _textractService.ExtractTextAsync(file);
        }
        else
        {
            return BadRequest(string.Format(ErrorMessages.UnsupportedFileType, ext));
        }

        if (string.IsNullOrWhiteSpace(extractedText))
            return BadRequest(ErrorMessages.NoExtractableText);

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

        // Assign tags if provided
        var parsedTagIds = _tagService.ParseTagIds(tagIds);
        var assignedTags = parsedTagIds.Count > 0
            ? await _tagService.AssignTagsToLogAsync(logId, parsedTagIds, userId)
            : new List<TagDto>();

        await _hubNotification.NotifyLogCreatedAsync(userId, logId, entity.FileName);

        return Ok(new { logId, tags = assignedTags });
    }

    /// <summary>
    /// Get a specific log
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var userId = GetUserId();

        var log = await _db.Logs.FindAsync(id);
        if (log == null) return NotFound();
        if (log.UserId != userId) return Forbid();

        var tags = await _tagService.GetTagsForLogAsync(id);

        return Ok(log.ToDto(tags));
    }

    /// <summary>
    /// Get log content
    /// </summary>
    [HttpGet("{id:guid}/content")]
    public IActionResult GetContent(Guid id)
    {
        var userId = GetUserId();

        var log = _db.Logs.Find(id);
        if (log == null) return NotFound();
        if (log.UserId != userId) return Forbid();

        if (!System.IO.File.Exists(log.StoragePath))
            return NotFound(ErrorMessages.FileMissing);

        return PhysicalFile(log.StoragePath, "text/plain");
    }

    /// <summary>
    /// List all logs for the current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? tagId = null)
    {
        var userId = GetUserId();

        var query = _db.Logs.AsNoTracking().Where(x => x.UserId == userId);

        if (tagId.HasValue)
        {
            var logIdsWithTag = _db.LogTagMappings
                .Where(m => m.TagId == tagId.Value)
                .Select(m => m.LogId);
            query = query.Where(l => logIdsWithTag.Contains(l.Id));
        }

        var logs = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(Defaults.MaxLogsPerList)
            .Select(x => new LogDto
            {
                Id = x.Id,
                FileName = x.FileName,
                SizeBytes = x.SizeBytes,
                CreatedAt = x.CreatedAt,
                Tags = _db.LogTagMappings
                    .Where(m => m.LogId == x.Id)
                    .Select(m => m.Tag.ToDto())
                    .ToList()
            })
            .ToListAsync();

        return Ok(logs);
    }

    /// <summary>
    /// Delete a log
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();

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

        await _hubNotification.NotifyLogDeletedAsync(userId!, id);

        return NoContent();
    }

    /// <summary>
    /// Bulk delete logs
    /// </summary>
    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (request.Ids == null || request.Ids.Count == 0)
            return BadRequestWithError(ErrorMessages.NoLogIdsProvided);

        var successCount = 0;
        var errors = new List<BulkOperationError>();

        foreach (var id in request.Ids)
        {
            try
            {
                var log = await _db.Logs.FindAsync(id);
                if (log == null)
                {
                    errors.Add(new BulkOperationError { Id = id, Error = ErrorMessages.LogNotFound });
                    continue;
                }
                if (log.UserId != userId)
                {
                    errors.Add(new BulkOperationError { Id = id, Error = ErrorMessages.AccessDenied });
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

    /// <summary>
    /// Bulk analyze logs
    /// </summary>
    [HttpPost("bulk-analyze")]
    public async Task<IActionResult> BulkAnalyze([FromBody] BulkAnalyzeRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (request.LogIds == null || request.LogIds.Count == 0)
            return BadRequestWithError(ErrorMessages.NoLogIdsProvided);

        var (allowed, message) = await _planEnforcement.CheckAnalysisAllowedAsync(userId, request.Model);
        if (!allowed)
            return PaymentRequired(message!);

        var token = GetBearerToken();
        if (token == null)
            return Unauthorized();

        var successCount = 0;
        var errors = new List<BulkOperationError>();

        foreach (var id in request.LogIds)
        {
            try
            {
                var log = await _db.Logs.FindAsync(id);
                if (log == null)
                {
                    errors.Add(new BulkOperationError { Id = id, Error = ErrorMessages.LogNotFound });
                    continue;
                }
                if (log.UserId != userId)
                {
                    errors.Add(new BulkOperationError { Id = id, Error = ErrorMessages.AccessDenied });
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

    /// <summary>
    /// Delete all logs
    /// </summary>
    [HttpDelete("all")]
    public async Task<IActionResult> DeleteAll()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

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

    /// <summary>
    /// Queue analysis for a log
    /// </summary>
    [HttpPost("{id:guid}/analyze")]
    public async Task<IActionResult> QueueAnalysis(Guid id, [FromBody] AnalyzeLogRequest? request = null)
    {
        var userId = GetUserId();

        var log = _db.Logs.Find(id);
        if (log == null) return NotFound();
        if (log.UserId != userId) return Forbid();

        var (allowed, message) = await _planEnforcement.CheckAnalysisAllowedAsync(userId!, request?.Model);
        if (!allowed)
            return PaymentRequired(message!);

        var token = GetBearerToken();
        if (token == null)
            return Unauthorized();

        await _analysisQueue.QueueAnalysisJobAsync(id, userId!, token, request?.Model);
        return Accepted(new { message = "Analysis queued", logId = id });
    }

    /// <summary>
    /// Analyze log content (legacy endpoint)
    /// </summary>
    [HttpGet("{id:guid}/analyze")]
    public async Task<IActionResult> AnalyseContent(Guid id, [FromQuery] string? model = null)
    {
        var userId = GetUserId();

        var log = await _db.Logs.FindAsync(id);
        if (log == null) return NotFound();
        if (log.UserId != userId) return Forbid();

        if (!System.IO.File.Exists(log.StoragePath))
            return NotFound(ErrorMessages.FileMissing);

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
            UserId = userId!,
            Title = $"Analysis Report – {log.FileName}",
            Summary = analysisText.Length > 500 ? analysisText[..500] : analysisText,
            ReportPath = reportPath,
            Status = ReportStatus.Completed,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Reports.Add(report);
        await _db.SaveChangesAsync();

        await _hubNotification.NotifyReportCreatedAsync(userId!, new ReportListItem
        {
            Id = report.Id,
            Title = report.Title,
            FileName = log.FileName,
            CreatedAtUtc = report.CreatedAtUtc,
            Status = ReportStatus.Completed
        });

        return Ok(analysis);
    }

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
