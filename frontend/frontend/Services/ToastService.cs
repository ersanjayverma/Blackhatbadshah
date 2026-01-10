namespace frontend.Services;

public class ToastService
{
    public event Action<ToastMessage>? OnShow;

    public void ShowSuccess(string message, string? title = null)
    {
        OnShow?.Invoke(new ToastMessage
        {
            Type = ToastType.Success,
            Title = title ?? "Success",
            Message = message
        });
    }

    public void ShowWarning(string message, string? title = null)
    {
        OnShow?.Invoke(new ToastMessage
        {
            Type = ToastType.Warning,
            Title = title ?? "Warning",
            Message = message
        });
    }

    public void ShowError(string message, string? title = null)
    {
        OnShow?.Invoke(new ToastMessage
        {
            Type = ToastType.Error,
            Title = title ?? "Error",
            Message = message
        });
    }

    public void ShowInfo(string message, string? title = null)
    {
        OnShow?.Invoke(new ToastMessage
        {
            Type = ToastType.Info,
            Title = title ?? "Info",
            Message = message
        });
    }
}

public class ToastMessage
{
    public ToastType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public enum ToastType
{
    Success,
    Warning,
    Error,
    Info
}
