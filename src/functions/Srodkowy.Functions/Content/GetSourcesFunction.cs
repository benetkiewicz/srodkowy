using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Srodkowy.Functions.Services;

namespace Srodkowy.Functions.Content;

public sealed class GetSourcesFunction(ContentReadService contentReadService)
{
    [Function("GetSources")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sources")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var sources = await contentReadService.GetSourcesAsync(cancellationToken);
        return await WriteJsonAsync(request, HttpStatusCode.OK, sources, cancellationToken);
    }

    private static async Task<HttpResponseData> WriteJsonAsync(HttpRequestData request, HttpStatusCode statusCode, object payload, CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(payload, cancellationToken);
        return response;
    }
}
