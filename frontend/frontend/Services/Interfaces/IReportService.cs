using shared.Dto;

namespace frontend.Services.Interfaces;

/// <summary>
/// Interface for report operations following Dependency Inversion principle.
/// </summary>
public interface IReportService
{
    Task<List<ReportListItem>> ListAsync();
    Task<ReportDetail?> GetAsync(Guid id);
    Task<Stream> DownloadAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<int> DeleteAllAsync();
}
