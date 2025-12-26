using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.SignalR.Client;

namespace frontend.Services;

public class HubConnectionService : IAsyncDisposable
{
    private readonly IAccessTokenProvider _tokenProvider;
    private HubConnection? _connection;
    private bool _isStarted;

    public HubConnectionService(IAccessTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    public HubConnection Connection => _connection ?? throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public event Action? OnReconnected;
    public event Action? OnReconnecting;
    public event Action<Exception?>? OnClosed;

    public async Task InitializeAsync()
    {
        if (_connection != null)
            return;

        _connection = new HubConnectionBuilder()
            .WithUrl("https://api.blackhatbadshah.com/hubs/data", options =>
            {
                options.AccessTokenProvider = async () =>
                {
                    var tokenResult = await _tokenProvider.RequestAccessToken();
                    if (tokenResult.TryGetToken(out var token))
                    {
                        return token.Value;
                    }
                    return string.Empty;
                };
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .Build();

        _connection.Reconnecting += error =>
        {
            OnReconnecting?.Invoke();
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            OnReconnected?.Invoke();
            return Task.CompletedTask;
        };

        _connection.Closed += error =>
        {
            OnClosed?.Invoke(error);
            _isStarted = false;
            return Task.CompletedTask;
        };
    }

    public async Task StartAsync()
    {
        if (_connection == null)
            await InitializeAsync();

        if (!_isStarted && _connection!.State == HubConnectionState.Disconnected)
        {
            await _connection.StartAsync();
            _isStarted = true;

            // Join user-specific group
            await _connection.InvokeAsync("JoinUserGroup");
        }
    }

    public async Task StopAsync()
    {
        if (_connection != null && _isStarted)
        {
            await _connection.StopAsync();
            _isStarted = false;
        }
    }

    public IDisposable On<T>(string methodName, Action<T> handler)
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

    public IDisposable On(string methodName, Action handler)
    {
        if (_connection == null)
            throw new InvalidOperationException("Hub connection not initialized. Call InitializeAsync first.");

        return _connection.On(methodName, handler);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
