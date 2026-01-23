using Microsoft.AspNetCore.SignalR;

namespace backend.Hubs;

/// <summary>
/// Hub filter that logs all exceptions from hub method invocations.
/// Helps diagnose server-side errors that cause client disconnections.
/// </summary>
public class HubExceptionFilter : IHubFilter
{
    private readonly ILogger<HubExceptionFilter> _logger;

    public HubExceptionFilter(ILogger<HubExceptionFilter> logger)
    {
        _logger = logger;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var hubName = invocationContext.Hub.GetType().Name;
        var methodName = invocationContext.HubMethodName;
        var connectionId = invocationContext.Context.ConnectionId;

        try
        {
            return await next(invocationContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "========== HUB METHOD EXCEPTION ==========\n" +
                "  Hub: {Hub}\n" +
                "  Method: {Method}\n" +
                "  ConnectionId: {ConnectionId}\n" +
                "  ExceptionType: {ExceptionType}\n" +
                "  Message: {Message}\n" +
                "  InnerException: {InnerException}\n" +
                "  StackTrace:\n{StackTrace}\n" +
                "  Arguments: {Arguments}\n" +
                "  Timestamp: {Timestamp}\n" +
                "==========================================",
                hubName,
                methodName,
                connectionId,
                ex.GetType().FullName,
                ex.Message,
                ex.InnerException?.Message ?? "N/A",
                ex.StackTrace ?? "N/A",
                FormatArguments(invocationContext.HubMethodArguments),
                DateTime.UtcNow.ToString("O"));

            // Re-throw to let SignalR handle it (will close connection for unhandled exceptions)
            throw;
        }
    }

    public Task OnConnectedAsync(
        HubLifetimeContext context,
        Func<HubLifetimeContext, Task> next)
    {
        _logger.LogDebug(
            "Hub connected: {Hub}, ConnectionId: {ConnectionId}",
            context.Hub.GetType().Name,
            context.Context.ConnectionId);
        
        return next(context);
    }

    public Task OnDisconnectedAsync(
        HubLifetimeContext context,
        Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        if (exception != null)
        {
            _logger.LogWarning(
                "Hub disconnected with exception: {Hub}, ConnectionId: {ConnectionId}, Error: {Error}",
                context.Hub.GetType().Name,
                context.Context.ConnectionId,
                exception.Message);
        }
        
        return next(context, exception);
    }

    private static string FormatArguments(IReadOnlyList<object?> arguments)
    {
        if (arguments == null || arguments.Count == 0)
            return "[]";

        try
        {
            var formatted = arguments.Select((arg, i) =>
            {
                if (arg == null) return $"[{i}]: null";
                
                var type = arg.GetType().Name;
                var value = arg.ToString();
                
                // Truncate long values
                if (value?.Length > 100)
                    value = value[..100] + "...";
                
                return $"[{i}]: ({type}) {value}";
            });

            return string.Join(", ", formatted);
        }
        catch
        {
            return "[Error formatting arguments]";
        }
    }
}
