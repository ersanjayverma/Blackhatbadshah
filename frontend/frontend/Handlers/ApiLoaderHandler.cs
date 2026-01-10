using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using frontend.Services;
public class ApiLoaderHandler : DelegatingHandler
{
    private readonly ApiLoaderService _loader;

    public ApiLoaderHandler(ApiLoaderService loader)
    {
        _loader = loader;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _loader.Start();

        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        finally
        {
            _loader.Stop();
        }
    }
}
