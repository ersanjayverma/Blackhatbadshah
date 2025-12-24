namespace frontend.Services;

public class ApiLoaderService
{
    private int _activeRequests = 0;

    public bool IsBusy => _activeRequests > 0;
    public event Action? OnChange;

    public void Start()
    {
        Interlocked.Increment(ref _activeRequests);
        Notify();
    }

    public void Stop()
    {
        Interlocked.Decrement(ref _activeRequests);
        Notify();
    }

    private void Notify() => OnChange?.Invoke();
}
