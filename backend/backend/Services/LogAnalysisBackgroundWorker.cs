using backend.Configuration;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using shared.Dto;
using System.Text;

namespace backend.Services;

public class LogAnalysisBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogAnalysisQueue _queue;
    private readonly ModelsConfig _modelsConfig;
    private readonly ILogger<LogAnalysisBackgroundWorker> _logger;
    private readonly ITokenValidationService _tokenValidationService;

    public LogAnalysisBackgroundWorker(
        IServiceProvider serviceProvider,
        ILogAnalysisQueue queue,
        IOptions<ModelsConfig> modelsConfig,
        ILogger<LogAnalysisBackgroundWorker> logger,
        ITokenValidationService tokenValidationService)
    {
        _serviceProvider = serviceProvider;
        _queue = queue;
        _modelsConfig = modelsConfig.Value;
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

                _logger.LogInformation(
                    "Processing analysis job for LogId: {LogId}, UserId: {UserId}, Model: {Model}",
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

    private async Task ProcessAnalysisJobAsync(
        Guid logId,
        string userId,
        string? model,
        CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var analyzer = scope.ServiceProvider.GetRequiredService<ILogAnalyzer>();
        var hubNotification = scope.ServiceProvider.GetRequiredService<IHubNotificationService>();
        var planEnforcement = scope.ServiceProvider.GetRequiredService<IPlanEnforcementService>();

        var storageRoot = config["Storage:RootPath"]
            ?? throw new InvalidOperationException("Storage:RootPath not configured");

        Guid? reportId = null;

        try
        {
            // 1. Fetch log
            var log = await db.Logs.FindAsync(new object[] { logId }, cancellationToken);
            if (log == null || log.UserId != userId)
            {
                _logger.LogWarning("Log {LogId} not found or user mismatch", logId);
                return;
            }

            // 2. Create report (InProgress)
            reportId = Guid.NewGuid();

            var reportDir = Path.Combine(storageRoot, "reports", log.Id.ToString());
            var chartDir = Path.Combine(storageRoot, "charts", log.Id.ToString());

            Directory.CreateDirectory(reportDir);
            Directory.CreateDirectory(chartDir);

            var reportPath = Path.Combine(reportDir, $"{reportId}.txt");
            var chartPath = Path.Combine(chartDir, $"{reportId}.json");

            var report = new Report
            {
                Id = reportId.Value,
                LogId = log.Id,
                UserId = userId,
                Title = $"Analysis Report – {log.FileName}",
                Summary = "Analysis in progress...",
                ReportPath = reportPath,
                ChartPath = chartPath,
                Model = model,
                Status = ReportStatus.InProgress,
                CreatedAtUtc = DateTime.UtcNow
            };

            db.Reports.Add(report);
            await db.SaveChangesAsync(cancellationToken);

            // 3. Notify creation
            await hubNotification.NotifyReportCreatedAsync(userId, new ReportListItem
            {
                Id = report.Id,
                Title = report.Title,
                FileName = log.FileName,
                CreatedAtUtc = report.CreatedAtUtc,
                Status = ReportStatus.InProgress
            });

            await hubNotification.NotifyReportStatusChangedAsync(
                userId, report.Id, ReportStatus.InProgress);

            // 4. Read log content from filesystem
            if (!System.IO.File.Exists(log.StoragePath))
            {
                await UpdateReportStatusAsync(
                    db, hubNotification, report.Id, userId,
                    ReportStatus.Failed, "Log content not found");
                return;
            }

            var logContent = await System.IO.File.ReadAllTextAsync(
                log.StoragePath, cancellationToken);

            if (string.IsNullOrWhiteSpace(logContent))
            {
                await UpdateReportStatusAsync(
                    db, hubNotification, report.Id, userId,
                    ReportStatus.Failed, "Log content is empty");
                return;
            }

            // 5. Analyze
            var analysis = await analyzer.AnalyzeAsync(log.Id, logContent, model);
            if (analysis == null)
            {
                await UpdateReportStatusAsync(
                    db, hubNotification, report.Id, userId,
                    ReportStatus.Failed, "Analysis returned no result");
                return;
            }

            var analysisText =
                analysis.reply ??
                analysis.ToString() ??
                throw new InvalidOperationException("Analysis produced no text");

            // 6. Write report to disk
            await System.IO.File.WriteAllTextAsync(
                reportPath, analysisText, Encoding.UTF8, cancellationToken);

            // 7. Generate chart
            var chart = await analyzer.AnalyzeAsync(
                log.Id, analysisText, _modelsConfig.ChartModel, isChart: true);

            var chartText = chart?.reply;
            if (string.IsNullOrWhiteSpace(chartText))
            {
                chartText = """
                {
                  "chartType": "None",
                  "title": "",
                  "xAxis": { "labels": [] },
                  "series": []
                }
                """;
            }

            await System.IO.File.WriteAllTextAsync(
                chartPath, chartText, Encoding.UTF8, cancellationToken);

            // 8. Finalize report
            report.Status = analysisText.StartsWith("Analyzer failed:", StringComparison.Ordinal)
                ? ReportStatus.Failed
                : ReportStatus.Completed;

            report.Summary = analysisText.Length > 500
                ? analysisText[..500]
                : analysisText;

            await db.SaveChangesAsync(cancellationToken);

            // 9. Record usage only on successful analysis
            if (report.Status == ReportStatus.Completed)
            {
                await planEnforcement.RecordAnalysisAsync(userId, model);
            }

            await hubNotification.NotifyReportStatusChangedAsync(
                userId, report.Id, report.Status);

            _logger.LogInformation(
                "Completed analysis for LogId {LogId}, ReportId {ReportId}",
                logId, reportId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing analysis for LogId {LogId}", logId);

            if (reportId.HasValue)
            {
                await UpdateReportStatusAsync(
                    db, hubNotification, reportId.Value, userId,
                    ReportStatus.Failed, $"Analysis failed: {ex.Message}");
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
            if (report == null)
                return;

            report.Status = status;
            report.Summary = summary;

            await db.SaveChangesAsync();
            await hubNotification.NotifyReportStatusChangedAsync(
                userId, reportId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to update report status for ReportId {ReportId}", reportId);
        }
    }
}
