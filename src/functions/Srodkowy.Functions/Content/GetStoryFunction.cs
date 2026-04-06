using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Srodkowy.Functions.Services;

namespace Srodkowy.Functions.Content;

public sealed class GetStoryFunction(ContentReadService contentReadService)
{
    [Function("GetStory")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "stories/{storyId:guid}")]
        HttpRequestData request,
        string storyId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(storyId, out var parsedStoryId))
        {
            return await WriteJsonAsync(request, HttpStatusCode.BadRequest, new { error = $"Invalid story id '{storyId}'." }, cancellationToken);
        }

        var story = await contentReadService.GetStoryAsync(parsedStoryId, cancellationToken);

        if (story is null)
        {
            return await WriteJsonAsync(request, HttpStatusCode.NotFound, new { error = $"Story '{parsedStoryId}' was not found." }, cancellationToken);
        }

        return await WriteJsonAsync(request, HttpStatusCode.OK, story, cancellationToken);
    }

    private static async Task<HttpResponseData> WriteJsonAsync(HttpRequestData request, HttpStatusCode statusCode, object payload, CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(payload, cancellationToken);
        return response;
    }
}
