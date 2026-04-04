using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Srodkowy.Functions.Services;

namespace Srodkowy.Functions.Tests;

public sealed class ArticleCleanupHeuristicsTests
{
    [Fact]
    public void NormalizeForCleanup_should_remove_duplicate_blocks_from_onet_samples()
    {
        var sample = GetSample("https://wiadomosci.onet.pl/kraj/rzecznik-praw-obywatelskich-walczy-o-emerytke-skarga-do-sadu-najwyzszego/zlvkrpk");

        var result = ArticleCleanupHeuristics.NormalizeForCleanup(sample.ContentMarkdown, maxInputCharacters: 24000);

        CountOccurrences(result.Markdown, "Donald Trump stawia nowe ultimatum Iranowi").Should().Be(1);
        result.Markdown.Should().Contain("Rzecznik Praw Obywatelskich walczy o emerytkę");
        result.Flags.Should().Contain("duplicate_block_removed");
    }

    [Fact]
    public void NormalizeForCleanup_should_preserve_main_content_even_when_embeds_are_present()
    {
        var sample = GetSample("https://www.radiomaryja.pl/informacje/wielkanocne-sniadanie-dla-potrzebujacych-w-sopocie-blisko-600-osob-przy-wspolnym-stole");

        var normalization = ArticleCleanupHeuristics.NormalizeForCleanup(sample.ContentMarkdown, maxInputCharacters: 24000);

        normalization.Markdown.Should().Contain("Wspólny stół, ciepły posiłek i to, co najważniejsze");

        var analysis = ArticleCleanupHeuristics.AnalyzeOutput(sample.Title, ArticleContentConverter.ToPlainText(normalization.Markdown), minCleanedLength: 320);
        analysis.QualityScore.Should().BeGreaterThan(30);
    }

    [Fact]
    public void AnalyzeOutput_should_flag_short_or_mismatched_outputs_for_review()
    {
        var title = "Thomas Sargent: otwartość kluczem do chińskiego cudu gospodarczego";
        const string cleanedText = "Krótka zajawka bez rozwinięcia.";

        var result = ArticleCleanupHeuristics.AnalyzeOutput(title, cleanedText, minCleanedLength: 320);

        result.NeedsReview.Should().BeTrue();
        result.Flags.Should().Contain("short_output");
        (result.Flags.Contains("low_word_count") || result.Flags.Contains("missing_sentences")).Should().BeTrue();
    }

    private static int CountOccurrences(string input, string value) =>
        Regex.Matches(input, Regex.Escape(value), RegexOptions.IgnoreCase).Count;

    private static SampleArticle GetSample(string url)
    {
        var json = File.ReadAllText(FindNoiseExamplesPath());
        var samples = JsonSerializer.Deserialize<List<SampleArticle>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Could not deserialize scraping noise examples.");

        return samples.Single(sample => string.Equals(sample.Url, url, StringComparison.OrdinalIgnoreCase));
    }

    private static string FindNoiseExamplesPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "functions", "Srodkowy.Functions.Tests", "Fixtures", "article-cleanup-samples.json");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Could not find docs/scraping-noise-examples.json.");
    }

    private sealed record SampleArticle(string Url, string Title, string ContentMarkdown);
}
