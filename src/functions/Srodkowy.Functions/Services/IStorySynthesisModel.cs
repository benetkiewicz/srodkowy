using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Srodkowy.Functions.Services;

public interface IStorySynthesisModel
{
    Task<StorySynthesisModelResponse> SynthesizeAsync(StorySynthesisModelRequest request, CancellationToken cancellationToken);
}

public sealed class OpenAiStorySynthesisModel(IChatClient chatClient) : IStorySynthesisModel
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<StorySynthesisModelResponse> SynthesizeAsync(StorySynthesisModelRequest request, CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(request);
        var response = await chatClient.GetResponseAsync<StorySynthesisModelResponse>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }

    private static string BuildPrompt(StorySynthesisModelRequest request)
    {
        var payload = JsonSerializer.Serialize(request, SerializerOptions);

        return $"""
You generate Polish story packages for a news product that compares how left and right media describe the same event.

Return strict JSON with this shape:
- headline: string
- synthesis: string
- markers: array of objects with phrase, kind, explanation
- left: object with summary and excerpts
- right: object with summary and excerpts

Rules:
- write all generated text in Polish
- write a calm, factual, restrained synthesis focused on the shared event core
- do not fact-check, moralize, speculate about motives, or pick a side
- headline should be concise and neutral
- synthesis should be about 150-300 words
- return between 1 and {request.MaxMarkers} markers
- each marker phrase must be copied exactly from the synthesis text as one contiguous substring
- marker phrases should be short and specific
- left.excerpts and right.excerpts must use exact wording copied from the provided article text as one contiguous substring
- every excerpt.articleId must be copied exactly from the input JSON as a UUID string with the same casing and hyphens
- every excerpt must reference a valid articleId from the same camp
- do not invent articleIds, sources, quotes, or facts
- do not normalize, shorten, paraphrase, inflect, or translate marker phrases or excerpts
- left and right summaries should describe the narrative framing of that camp, not the truth of the event

Input JSON:
{payload}
""";
    }
}

public sealed record StorySynthesisModelRequest(
    Guid CandidateClusterId,
    int Rank,
    int MaxMarkers,
    IReadOnlyList<StorySynthesisArticleInput> Articles);

public sealed record StorySynthesisArticleInput(
    Guid ArticleId,
    string SourceName,
    string Camp,
    string Url,
    DateTimeOffset? PublishedAt,
    string Title,
    string CleanedContentText);

public sealed record StorySynthesisModelResponse(
    string Headline,
    string Synthesis,
    IReadOnlyList<StorySynthesisMarkerResponse>? Markers,
    StorySynthesisSideResponse? Left,
    StorySynthesisSideResponse? Right);

public sealed record StorySynthesisMarkerResponse(
    string Phrase,
    string Kind,
    string Explanation);

public sealed record StorySynthesisSideResponse(
    string Summary,
    IReadOnlyList<StorySynthesisExcerptResponse>? Excerpts);

public sealed record StorySynthesisExcerptResponse(
    string ArticleId,
    string Text);
