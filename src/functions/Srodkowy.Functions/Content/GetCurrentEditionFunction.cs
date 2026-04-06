using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Srodkowy.Functions.Services;

namespace Srodkowy.Functions.Content;

public sealed class GetCurrentEditionFunction(ContentReadService contentReadService)
{
    [Function("GetCurrentEdition")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "editions/current")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var edition = await contentReadService.GetCurrentEditionAsync(cancellationToken);

        if (edition is null)
        {
            return await WriteJsonAsync(request, HttpStatusCode.NotFound, new { error = "No live edition was found." }, cancellationToken);
        }

        return await WriteJsonAsync(request, HttpStatusCode.OK, edition, cancellationToken);
    }

    private static async Task<HttpResponseData> WriteJsonAsync(HttpRequestData request, HttpStatusCode statusCode, object payload, CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(payload, cancellationToken);
        return response;
    }
}
