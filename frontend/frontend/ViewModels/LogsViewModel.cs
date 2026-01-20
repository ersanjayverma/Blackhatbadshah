using frontend.Services.Interfaces;
using shared.Dto;

namespace frontend.ViewModels;

/// <summary>
/// ViewModel for Logs page following MVVM pattern.
/// Manages state and data loading for the logs view.
/// </summary>
public class LogsViewModel
{
    private readonly ILogService _logService;

    public bool IsLoading { get; private set; }
    public bool IsUploading { get; private set; }
    public bool IsDeleting { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }
    public List<LogDto> Logs { get; private set; } = new();
    public LogDto? SelectedLog { get; private set; }
    public string? SelectedLogContent { get; private set; }

    public event Action? StateChanged;

    public LogsViewModel(ILogService logService)
    {
        _logService = logService;
    }

    public async Task LoadLogsAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        NotifyStateChanged();

        try
        {
            Logs = await _logService.ListAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load logs: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public async Task<bool> UploadAsync(Stream fileStream, string fileName, string contentType)
    {
        IsUploading = true;
        ErrorMessage = null;
        SuccessMessage = null;
        NotifyStateChanged();

        try
        {
            await _logService.UploadAsync(fileStream, fileName, contentType);
            SuccessMessage = $"Successfully uploaded {fileName}";
            await LoadLogsAsync();
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to upload: {ex.Message}";
            return false;
        }
        finally
        {
            IsUploading = false;
            NotifyStateChanged();
        }
    }

    public async Task SelectLogAsync(Guid logId)
    {
        IsLoading = true;
        NotifyStateChanged();

        try
        {
            SelectedLog = await _logService.GetAsync(logId);
            if (SelectedLog != null)
            {
                SelectedLogContent = await _logService.GetContentAsync(logId);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load log content: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public async Task<bool> DeleteAsync(Guid logId)
    {
        IsDeleting = true;
        ErrorMessage = null;
        NotifyStateChanged();

        try
        {
            await _logService.DeleteAsync(logId);
            Logs.RemoveAll(l => l.Id == logId);
            if (SelectedLog?.Id == logId)
            {
                SelectedLog = null;
                SelectedLogContent = null;
            }
            SuccessMessage = "Log deleted successfully";
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete log: {ex.Message}";
            return false;
        }
        finally
        {
            IsDeleting = false;
            NotifyStateChanged();
        }
    }

    public async Task<QueueAnalysisResponse?> QueueAnalysisAsync(Guid logId, string? model = null)
    {
        IsLoading = true;
        ErrorMessage = null;
        NotifyStateChanged();

        try
        {
            return await _logService.QueueAnalysisAsync(logId, model);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to queue analysis: {ex.Message}";
            return null;
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public void ClearSelection()
    {
        SelectedLog = null;
        SelectedLogContent = null;
        NotifyStateChanged();
    }

    public void ClearMessages()
    {
        ErrorMessage = null;
        SuccessMessage = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
