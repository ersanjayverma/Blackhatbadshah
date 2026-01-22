using System.Net;
using System.Text.Json;

namespace backend.Middleware;

/// <summary>
/// Global exception handling middleware for consistent error responses.
/// Catches unhandled exceptions and returns standardized JSON error responses.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected - don't log as error
            _logger.LogDebug("Request cancelled by client: {Path}", context.Request.Path);
            context.Response.StatusCode = 499; // Client Closed Request
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;
        
        // Log the exception with context
        _logger.LogError(
            exception,
            "Unhandled exception [{TraceId}] {Method} {Path}: {Message}",
            traceId,
            context.Request.Method,
            context.Request.Path,
            exception.Message);

        // Determine status code and message based on exception type
        var (statusCode, message, errorCode) = exception switch
        {
            ArgumentException or ArgumentNullException 
                => (HttpStatusCode.BadRequest, exception.Message, "INVALID_ARGUMENT"),
            
            UnauthorizedAccessException 
                => (HttpStatusCode.Unauthorized, "Authentication required", "UNAUTHORIZED"),
            
            KeyNotFoundException 
                => (HttpStatusCode.NotFound, "The requested resource was not found", "NOT_FOUND"),
            
            InvalidOperationException 
                => (HttpStatusCode.Conflict, exception.Message, "INVALID_OPERATION"),
            
            TimeoutException 
                => (HttpStatusCode.GatewayTimeout, "The operation timed out", "TIMEOUT"),
            
            NotSupportedException 
                => (HttpStatusCode.NotImplemented, "This operation is not supported", "NOT_SUPPORTED"),
            
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred", "INTERNAL_ERROR")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new ErrorResponse
        {
            Error = message,
            ErrorCode = errorCode,
            TraceId = traceId,
            Timestamp = DateTime.UtcNow,
            Path = context.Request.Path
        };

        // Include stack trace in development
        if (_environment.IsDevelopment() && statusCode == HttpStatusCode.InternalServerError)
        {
            response.Details = exception.ToString();
        }

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment()
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
}

/// <summary>
/// Standardized error response format.
/// </summary>
public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Path { get; set; } = string.Empty;
    public string? Details { get; set; }
}

/// <summary>
/// Extension methods for registering the middleware.
/// </summary>
public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
