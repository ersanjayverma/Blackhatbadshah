using backend.Configuration;
using backend.Data;
using backend.Data.Entities;
using backend.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using shared.Dto;
using System.Text;

namespace backend.Services;

public class LiveLogAnalysisBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILiveLogAnalysisQueue _queue;
    private readonly IHubNotificationService _hubNotification;
    private readonly ModelsConfig _modelsConfig;
    private readonly ILogger<LiveLogAnalysisBackgroundWorker> _logger;

    public LiveLogAnalysisBackgroundWorker(
        IServiceProvider serviceProvider,
        ILiveLogAnalysisQueue queue,
        IHubNotificationService hubNotification,
        IOptions<ModelsConfig> modelsConfig,
        ILogger<LiveLogAnalysisBackgroundWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _queue = queue;
        _hubNotification = hubNotification;
        _modelsConfig = modelsConfig.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LiveLogAnalysisBackgroundWorker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _queue.DequeueAsync(stoppingToken);
                if (job == null)
                    continue;

                _logger.LogInformation(
                    "Processing live log analysis job for Session: {SessionId}, User: {UserId}, Chunk: {ChunkNumber}, Model: {Model}",
                    job.SessionId, job.UserId, job.ChunkNumber, job.Model ?? "default");

                TokenContextService.CurrentToken = job.AccessToken;

                try
                {
                    await ProcessLiveLogAnalysisAsync(job, stoppingToken);
                }
                finally
                {
                    TokenContextService.CurrentToken = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing live log analysis job.");
            }
        }

        _logger.LogInformation("LiveLogAnalysisBackgroundWorker is stopping.");
    }

    private async Task ProcessLiveLogAnalysisAsync(LiveLogAnalysisJob job, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var analyzer = scope.ServiceProvider.GetRequiredService<ILogAnalyzer>();
        var planEnforcement = scope.ServiceProvider.GetRequiredService<IPlanEnforcementService>();

        var storageRoot = config["Storage:RootPath"]
            ?? throw new InvalidOperationException("Storage:RootPath not configured");

        Guid reportId = Guid.NewGuid();

        try
        {
            // Notify frontend that analysis started
            await _hubNotification.NotifyLiveLogAnalysisStartedAsync(job.WorkerId, reportId, job.ChunkNumber);

            // Create directories for live log reports
            var liveLogDir = Path.Combine(storageRoot, "livelog-reports", job.UserId);
            var chartDir = Path.Combine(storageRoot, "livelog-charts", job.UserId);

            Directory.CreateDirectory(liveLogDir);
            Directory.CreateDirectory(chartDir);

            var reportPath = Path.Combine(liveLogDir, $"{reportId}.txt");
            var chartPath = Path.Combine(chartDir, $"{reportId}.json");

            // Create report record
            var report = new Report
            {
                Id = reportId,
                LogId = null, // Live logs don't have a persistent log file
                UserId = job.UserId,
                Title = $"Live Log Analysis – Chunk {job.ChunkNumber}",
                Summary = "Analysis in progress...",
                ReportPath = reportPath,
                ChartPath = chartPath,
                Model = job.Model,
                Status = ReportStatus.InProgress,
                CreatedAtUtc = DateTime.UtcNow
            };

            db.Reports.Add(report);
            await db.SaveChangesAsync(cancellationToken);

            // Analyze the log content
            var analysis = await analyzer.AnalyzeAsync(
                reportId,
                job.LogContent,
                job.Model);

            if (analysis == null)
            {
                await UpdateReportAndNotifyAsync(
                    db, job, reportId, ReportStatus.Failed,
                    "Analysis returned no result", reportPath);
                return;
            }

            var analysisText = analysis.reply
                ?? analysis.ToString()
                ?? throw new InvalidOperationException("Analysis produced no text");

            // Write report to disk
            await File.WriteAllTextAsync(reportPath, analysisText, Encoding.UTF8, cancellationToken);

            // Generate chart
            var chart = await analyzer.AnalyzeAsync(
                reportId,
                analysisText,
                _modelsConfig.ChartModel,
                isChart: true);

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

            await File.WriteAllTextAsync(chartPath, chartText, Encoding.UTF8, cancellationToken);

            // Finalize report
            report.Status = analysisText.StartsWith("Analyzer failed:", StringComparison.Ordinal)
                ? ReportStatus.Failed
                : ReportStatus.Completed;

            report.Summary = analysisText.Length > 500
                ? analysisText[..500]
                : analysisText;

            await db.SaveChangesAsync(cancellationToken);

            // Record usage only on successful analysis
            if (report.Status == ReportStatus.Completed)
            {
                await planEnforcement.RecordAnalysisAsync(job.UserId, job.Model);
            }

            // Notify frontend of completion
            await _hubNotification.NotifyLiveLogAnalysisCompletedAsync(
                job.WorkerId, reportId, job.ChunkNumber, report.Status.ToString(), report.Summary);

            _logger.LogInformation(
                "Completed live log analysis for Session {SessionId}, Worker {WorkerId}, Chunk {ChunkNumber}, ReportId {ReportId}",
                job.SessionId, job.WorkerId, job.ChunkNumber, reportId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing live log analysis for Session {SessionId}, Worker {WorkerId}, Chunk {ChunkNumber}",
                job.SessionId, job.WorkerId, job.ChunkNumber);

            await _hubNotification.NotifyLiveLogAnalysisFailedAsync(
                job.WorkerId, reportId, job.ChunkNumber, ex.Message);

            // Update report status in database
            try
            {
                var report = await db.Reports.FindAsync(reportId);
                if (report != null)
                {
                    report.Status = ReportStatus.Failed;
                    report.Summary = $"Analysis failed: {ex.Message}";
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update report status for ReportId {ReportId}", reportId);
            }
        }
    }

    private async Task UpdateReportAndNotifyAsync(
        AppDbContext db,
        LiveLogAnalysisJob job,
        Guid reportId,
        ReportStatus status,
        string summary,
        string reportPath)
    {
        try
        {
            var report = await db.Reports.FindAsync(reportId);
            if (report != null)
            {
                report.Status = status;
                report.Summary = summary;
                await db.SaveChangesAsync();
            }

            if (status == ReportStatus.Completed)
            {
                await _hubNotification.NotifyLiveLogAnalysisCompletedAsync(
                    job.WorkerId, reportId, job.ChunkNumber, status.ToString(), summary);
            }
            else
            {
                await _hubNotification.NotifyLiveLogAnalysisFailedAsync(
                    job.WorkerId, reportId, job.ChunkNumber, summary);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update report and notify for ReportId {ReportId}", reportId);
        }
    }
}
