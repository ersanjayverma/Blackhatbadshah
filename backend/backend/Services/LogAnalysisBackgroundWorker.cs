using Azure.Storage.Blobs;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using shared.Dto;
using System.Text;

namespace backend.Services;

public class LogAnalysisBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogAnalysisQueue _queue;
    private readonly ILogger<LogAnalysisBackgroundWorker> _logger;
    private readonly ITokenValidationService _tokenValidationService;

    public LogAnalysisBackgroundWorker(
        IServiceProvider serviceProvider,
        ILogAnalysisQueue queue,
        ILogger<LogAnalysisBackgroundWorker> logger,
        ITokenValidationService tokenValidationService)
    {
        _serviceProvider = serviceProvider;
        _queue = queue;
        _logger = logger;
        _tokenValidationService = tokenValidationService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LogAnalysisBackgroundWorker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _queue.DequeueAsync(stoppingToken);

                if (job == null)
                    continue;

                var (logId, userId, accessToken, model) = job.Value;

                _logger.LogInformation("Processing analysis job for LogId: {LogId}, UserId: {UserId}, Model: {Model}",
                    logId, userId, model ?? ModelMapping.DefaultModel);

                // ✅ SECURITY: Validate that the userId in the token matches the queued userId
                var tokenUserId = _tokenValidationService.ExtractUserId(accessToken);
                if (string.IsNullOrEmpty(tokenUserId))
                {
                    _logger.LogError("Failed to extract userId from access token for LogId: {LogId}", logId);
                    continue; // Skip this job - invalid token
                }

                if (tokenUserId != userId)
                {
                    _logger.LogError(
                        "SECURITY VIOLATION: Token userId ({TokenUserId}) does not match queued userId ({QueuedUserId}) for LogId: {LogId}",
                        tokenUserId, userId, logId);
                    continue; // Skip this job - potential security breach
                }

                _logger.LogInformation("Token validation successful: userId matches for LogId: {LogId}", logId);

                // ✅ Set user's token in async context for handler to use
                TokenContextService.CurrentToken = accessToken;

                try
                {
                    await ProcessAnalysisJobAsync(logId, userId, model, stoppingToken);
                }
                finally
                {
                    // ✅ Clear token after processing
                    TokenContextService.CurrentToken = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing analysis job.");
            }
        }

        _logger.LogInformation("LogAnalysisBackgroundWorker is stopping.");
    }

    private async Task ProcessAnalysisJobAsync(Guid logId, string userId, string? model, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var blobService = scope.ServiceProvider.GetRequiredService<BlobServiceClient>();
        var analyzer = scope.ServiceProvider.GetRequiredService<ILogAnalyzer>();
        var hubNotification = scope.ServiceProvider.GetRequiredService<IHubNotificationService>();

        Guid? reportId = null;

        try
        {
            // 1. Fetch log
            var log = await db.Logs.FindAsync(new object[] { logId }, cancellationToken);
            if (log == null)
            {
                _logger.LogWarning("Log {LogId} not found", logId);
                return;
            }

            if (log.UserId != userId)
            {
                _logger.LogWarning("UserId mismatch for log {LogId}", logId);
                return;
            }

            // 2. Create report with "InProgress" status
            reportId = Guid.NewGuid();
            var report = new Report
            {
                Id = reportId.Value,
                LogId = log.Id,
                UserId = userId,
                Title = $"Analysis Report – {log.FileName}",
                Summary = "Analysis in progress...",
                ReportPath = $"reports/{log.Id}/{reportId}.txt",
                Status = ReportStatus.InProgress,
                CreatedAtUtc = DateTime.UtcNow
            };

            db.Reports.Add(report);
            await db.SaveChangesAsync(cancellationToken);

            // 3. Notify that report is created and in progress
            var reportListItem = new ReportListItem
            {
                Id = report.Id,
                Title = report.Title,
                FileName = log.FileName,
                CreatedAtUtc = report.CreatedAtUtc,
                Status = ReportStatus.InProgress
            };
            await hubNotification.NotifyReportCreatedAsync(userId, reportListItem);
            await hubNotification.NotifyReportStatusChangedAsync(userId, reportId.Value, ReportStatus.InProgress);

            // 4. Fetch log content from blob
            var containerName = config["AzureBlob:Container"]
                ?? throw new InvalidOperationException("AzureBlob:Container missing");

            var container = blobService.GetBlobContainerClient(containerName);
            var logBlob = container.GetBlobClient(log.StoragePath);

            if (!await logBlob.ExistsAsync(cancellationToken))
            {
                _logger.LogWarning("Log content missing in storage for LogId: {LogId}", logId);
                await UpdateReportStatusAsync(db, hubNotification, reportId.Value, userId, ReportStatus.Failed, "Log content not found in storage");
                return;
            }

            string logContent;
            await using (var stream = await logBlob.OpenReadAsync(cancellationToken: cancellationToken))
            using (var reader = new StreamReader(stream))
            {
                logContent = await reader.ReadToEndAsync(cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(logContent))
            {
                _logger.LogWarning("Log content is empty for LogId: {LogId}", logId);
                await UpdateReportStatusAsync(db, hubNotification, reportId.Value, userId, ReportStatus.Failed, "Log content is empty");
                return;
            }

            // 5. Analyze log with specified model
            var analysis = await analyzer.AnalyzeAsync(log.Id, logContent, model);
            if (analysis == null)
            {
                _logger.LogWarning("Analyzer returned no result for LogId: {LogId}", logId);
                await UpdateReportStatusAsync(db, hubNotification, reportId.Value, userId, ReportStatus.Failed, "Analysis failed to produce results");
                return;
            }

            // 6. Extract text
            var analysisText = analysis.reply
                ?? analysis.ToString()
                ?? throw new InvalidOperationException("Analysis produced no text");

            // 7. Upload analysis to blob
            await using (var textStream = new MemoryStream(Encoding.UTF8.GetBytes(analysisText)))
            {
                await container
                    .GetBlobClient(report.ReportPath)
                    .UploadAsync(textStream, overwrite: true, cancellationToken: cancellationToken);
            }

            // 8. Update report status to completed
            report.Status = ReportStatus.Completed;
            report.Summary = analysisText.Length > 500
                ? analysisText[..500]
                : analysisText;

            await db.SaveChangesAsync(cancellationToken);

            // 9. Notify completion
            await hubNotification.NotifyReportStatusChangedAsync(userId, reportId.Value, ReportStatus.Completed);

            _logger.LogInformation("Successfully completed analysis for LogId: {LogId}, ReportId: {ReportId}", logId, reportId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing analysis for LogId: {LogId}", logId);

            if (reportId.HasValue)
            {
                await UpdateReportStatusAsync(db, hubNotification, reportId.Value, userId, ReportStatus.Failed, $"Analysis failed: {ex.Message}");
            }
        }
    }

    private async Task UpdateReportStatusAsync(
        AppDbContext db,
        IHubNotificationService hubNotification,
        Guid reportId,
        string userId,
        ReportStatus status,
        string summary)
    {
        try
        {
            var report = await db.Reports.FindAsync(reportId);
            if (report != null)
            {
                report.Status = status;
                report.Summary = summary;
                await db.SaveChangesAsync();

                await hubNotification.NotifyReportStatusChangedAsync(userId, reportId, status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update report status for ReportId: {ReportId}", reportId);
        }
    }
}
