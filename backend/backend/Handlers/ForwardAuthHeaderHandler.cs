using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
namespace backend.Handlers;
public sealed class ForwardAuthHeaderHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ForwardAuthHeaderHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext != null &&
            httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            // Forward Authorization header exactly as received
            request.Headers.Authorization =
                AuthenticationHeaderValue.Parse(authHeader!);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
