using System.Net.Http.Json;
using shared.Dto;

namespace frontend.Services;

public class ReportService
{
    private readonly HttpClient _http;

    public ReportService(HttpClient http)
    {
        _http = http;
    }

    // -------------------------------------------------
    // GET /api/reports
    // List current user's reports
    // -------------------------------------------------
    public async Task<List<ReportListItem>> ListAsync()
    {
        return await _http.GetFromJsonAsync<List<ReportListItem>>("/api/reports")
               ?? new List<ReportListItem>();
    }

    // -------------------------------------------------
    // GET /api/reports/{id}
    // Get report detail with content
    // -------------------------------------------------
    public async Task<ReportDetail?> GetAsync(Guid id)
    {
        return await _http.GetFromJsonAsync<ReportDetail>($"/api/reports/{id}");
    }

    // -------------------------------------------------
    // GET /api/reports/{id}/download
    // Download report as text file
    // -------------------------------------------------
    public async Task<Stream> DownloadAsync(Guid id)
    {
        var response = await _http.GetAsync($"/api/reports/{id}/download");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync();
    }

    // -------------------------------------------------
    // DELETE /api/reports/{id}
    // Delete a single report
    // -------------------------------------------------
    public async Task DeleteAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"/api/reports/{id}");
        response.EnsureSuccessStatusCode();
    }

    // -------------------------------------------------
    // DELETE /api/reports/all
    // Delete all reports for the current user
    // -------------------------------------------------
    public async Task<int> DeleteAllAsync()
    {
        var response = await _http.DeleteAsync("/api/reports/all");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<DeleteAllResponse>();
        return result?.Deleted ?? 0;
    }
}
