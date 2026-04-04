using System.Text.RegularExpressions;

namespace Srodkowy.Functions.Services;

public static partial class ArticleCleanupHeuristics
{
    public const string CleanupProcessorVersion = "llm-cleanup-v1";

    public static NormalizationResult NormalizeForCleanup(string markdown, int maxInputCharacters)
    {
        var normalizedBlocks = new List<string>();
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenBlocks = new HashSet<string>(StringComparer.Ordinal);

        foreach (var block in SplitBlocks(markdown))
        {
            var normalizedBlock = NormalizeBlock(block);

            if (string.IsNullOrWhiteSpace(normalizedBlock))
            {
                continue;
            }

            var dedupeKey = NormalizeWhitespace(ArticleContentConverter.ToPlainText(normalizedBlock)).ToLowerInvariant();

            if (dedupeKey.Length >= 40 && !seenBlocks.Add(dedupeKey))
            {
                flags.Add("duplicate_block_removed");
                continue;
            }

            normalizedBlocks.Add(normalizedBlock);
        }

        var normalizedMarkdown = string.Join(Environment.NewLine + Environment.NewLine, normalizedBlocks).Trim();

        if (normalizedMarkdown.Length > maxInputCharacters)
        {
            normalizedMarkdown = normalizedMarkdown[..maxInputCharacters].TrimEnd();
            flags.Add("input_truncated");
        }

        return new NormalizationResult(normalizedMarkdown, [.. flags.OrderBy(flag => flag)]);
    }

    public static Analysis AnalyzeOutput(string title, string cleanedText, int minCleanedLength)
    {
        var normalizedText = NormalizeWhitespace(cleanedText);
        var titleTokens = Tokenize(title);
        var textTokens = Tokenize(normalizedText);
        var titleOverlap = ComputeOverlap(titleTokens, textTokens);
        var wordCount = textTokens.Count;
        var sentenceCount = SentenceRegex().Matches(normalizedText).Count;
        var flags = new List<string>();

        if (normalizedText.Length < minCleanedLength)
        {
            flags.Add("short_output");
        }

        if (sentenceCount == 0)
        {
            flags.Add("missing_sentences");
        }

        if (titleTokens.Count > 0 && titleOverlap < 0.15)
        {
            flags.Add("low_title_overlap");
        }

        if (wordCount < 60)
        {
            flags.Add("low_word_count");
        }

        var qualityScore = Math.Clamp(
            20 + Math.Min(45, wordCount / 8) + Math.Min(20, sentenceCount * 3) + (int)Math.Round(titleOverlap * 15) - (flags.Count * 8),
            0,
            100);

        var needsReview = normalizedText.Length < minCleanedLength || sentenceCount == 0;
        return new Analysis(NormalizeParagraphs(cleanedText), qualityScore, needsReview, [.. flags]);
    }

    private static IEnumerable<string> SplitBlocks(string markdown)
    {
        return markdown
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(block => !string.IsNullOrWhiteSpace(block));
    }

    private static string NormalizeBlock(string block)
    {
        return block
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();
    }

    private static string NormalizeParagraphs(string text)
    {
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            text.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.TrimEntries)
                .Select(ArticleContentConverter.ToPlainText)
                .Select(paragraph => paragraph.Trim())
                .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph)));
    }

    private static double ComputeOverlap(IReadOnlySet<string> titleTokens, IReadOnlySet<string> contentTokens)
    {
        if (titleTokens.Count == 0 || contentTokens.Count == 0)
        {
            return 0d;
        }

        var overlap = titleTokens.Count(contentTokens.Contains);
        return (double)overlap / titleTokens.Count;
    }

    private static string NormalizeWhitespace(string input) => WhitespaceRegex().Replace(input, " ").Trim();

    private static HashSet<string> Tokenize(string input)
    {
        return TokenRegex()
            .Matches(input.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(token => token.Length >= 4)
            .ToHashSet(StringComparer.Ordinal);
    }

    public sealed record NormalizationResult(string Markdown, IReadOnlyList<string> Flags);

    public sealed record Analysis(string CleanedText, int QualityScore, bool NeedsReview, IReadOnlyList<string> Flags);

    [GeneratedRegex("[.!?…]+")]
    private static partial Regex SentenceRegex();

    [GeneratedRegex("\\p{L}[\\p{L}\\p{Mn}\\-']+")]
    private static partial Regex TokenRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
