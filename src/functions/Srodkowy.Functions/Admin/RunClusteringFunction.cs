using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Srodkowy.Functions.Services;

namespace Srodkowy.Functions.Admin;

public sealed class RunClusteringFunction(CandidateClusteringService clusteringService)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Function("RunClustering")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "ops/clusters/run")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var payload = await ReadRequestAsync(request, cancellationToken);
        var result = await clusteringService.RunAsync("http-ops-clusters", payload, cancellationToken);
        return await WriteJsonAsync(request, HttpStatusCode.OK, result, cancellationToken);
    }

    private static async Task<CandidateClusteringService.ClusteringRunRequest?> ReadRequestAsync(
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        return JsonSerializer.Deserialize<CandidateClusteringService.ClusteringRunRequest>(body, SerializerOptions);
    }

    private static async Task<HttpResponseData> WriteJsonAsync(
        HttpRequestData request,
        HttpStatusCode statusCode,
        object payload,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(payload, cancellationToken);
        return response;
    }
}
