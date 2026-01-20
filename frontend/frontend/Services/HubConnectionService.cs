using Microsoft.AspNetCore.SignalR.Client;

namespace frontend.Services;

public class HubConnectionService : IAsyncDisposable
{
    private HubConnection? _connection;
    private bool _isStarted;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private Func<Task<string?>>? _tokenProvider;

    public HubConnection Connection => _connection ?? throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public HubConnectionState? State => _connection?.State;

    public event Action? OnReconnected;
    public event Action? OnReconnecting;
    public event Action<Exception?>? OnClosed;
    public event Action<bool>? OnConnectionStateChanged;

    public async Task InitializeAsync(Func<Task<string?>> tokenProviderFunc)
    {
        await _startLock.WaitAsync();
        try
        {
            if (_connection != null)
                return;

            _tokenProvider = tokenProviderFunc;

            _connection = new HubConnectionBuilder()
                .WithUrl("https://api.blackhatbadshah.com/hubs/data", options =>
                {
                    options.AccessTokenProvider = async () =>
                    {
                        if (_tokenProvider != null)
                        {
                            return await _tokenProvider() ?? string.Empty;
                        }
                        return string.Empty;
                    };
                })
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                .Build();

            _connection.Reconnecting += error =>
            {
                OnReconnecting?.Invoke();
                OnConnectionStateChanged?.Invoke(false);
                return Task.CompletedTask;
            };

            _connection.Reconnected += async connectionId =>
            {
                // Rejoin user group after reconnection
                try
                {
                    await _connection.InvokeAsync("JoinUserGroup");
                    OnReconnected?.Invoke();
                    OnConnectionStateChanged?.Invoke(true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to rejoin user group: {ex.Message}");
                }
            };

            _connection.Closed += error =>
            {
                OnClosed?.Invoke(error);
                OnConnectionStateChanged?.Invoke(false);
                _isStarted = false;
                return Task.CompletedTask;
            };
        }
        finally
        {
            _startLock.Release();
        }
    }

    public async Task StartAsync()
    {
        await _startLock.WaitAsync();
        try
        {
            if (_connection == null)
                throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first with a token provider.");

            if (!_isStarted && _connection.State == HubConnectionState.Disconnected)
            {
                await _connection.StartAsync();
                _isStarted = true;
                OnConnectionStateChanged?.Invoke(true);

                // Join user-specific group
                await _connection.InvokeAsync("JoinUserGroup");
            }
        }
        finally
        {
            _startLock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _startLock.WaitAsync();
        try
        {
            if (_connection != null && _isStarted)
            {
                await _connection.StopAsync();
                _isStarted = false;
            }
        }
        finally
        {
            _startLock.Release();
        }
    }

    public IDisposable On<T>(string methodName, Action<T> handler)
    {
        if (_connection == null)
            throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");

        return _connection.On(methodName, handler);
    }

    public IDisposable On<T>(string methodName, Func<T, Task> handler)
    {
        if (_connection == null)
            throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");

        return _connection.On(methodName, handler);
    }

    public IDisposable On<T1, T2>(string methodName, Action<T1, T2> handler)
    {
        if (_connection == null)
            throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");

        return _connection.On(methodName, handler);
    }

    public IDisposable On<T1, T2>(string methodName, Func<T1, T2, Task> handler)
    {
        if (_connection == null)
            throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");

        return _connection.On(methodName, handler);
    }

    public IDisposable On<T1, T2, T3>(string methodName, Action<T1, T2, T3> handler)
    {
        if (_connection == null)
            throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");

        return _connection.On(methodName, handler);
    }

    public IDisposable On(string methodName, Action handler)
    {
        if (_connection == null)
            throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");

        return _connection.On(methodName, handler);
    }

    public IDisposable On(string methodName, Func<Task> handler)
    {
        if (_connection == null)
            throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");

        return _connection.On(methodName, handler);
    }

    public async Task InvokeAsync(string methodName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");

        await _connection.InvokeAsync(methodName);
    }

    public async Task InvokeAsync<T>(string methodName, T arg)
    {
        if (_connection == null)
            throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");

        await _connection.InvokeAsync(methodName, arg);
    }

    public async Task InvokeAsync<T1, T2>(string methodName, T1 arg1, T2 arg2)
    {
        if (_connection == null)
            throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");

        await _connection.InvokeAsync(methodName, arg1, arg2);
    }

    public async Task<TResult> InvokeAsync<TResult>(string methodName)
    {
        if (_connection == null)
            throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");

        return await _connection.InvokeAsync<TResult>(methodName);
    }

    public async Task<TResult> InvokeAsync<TResult, T>(string methodName, T arg)
    {
        if (_connection == null)
            throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");

        return await _connection.InvokeAsync<TResult>(methodName, arg);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
        _startLock.Dispose();
    }
}
