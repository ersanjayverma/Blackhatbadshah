using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using backend.Services;

namespace backend.Handlers;

public sealed class ForwardAuthHeaderHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ForwardAuthHeaderHandler> _logger;

    public ForwardAuthHeaderHandler(
        IHttpContextAccessor httpContextAccessor,
        ILogger<ForwardAuthHeaderHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext != null &&
            httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            // ✅ Forward Authorization header from HTTP request
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader!);
            _logger.LogDebug("Forwarding user auth token from HTTP context");
        }
        else
        {
            // ✅ No HTTP context (background worker) - use token from async context
            var token = TokenContextService.CurrentToken;
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                _logger.LogDebug("Using user token from background worker context");
            }
            else
            {
                _logger.LogWarning("No auth token available");
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
