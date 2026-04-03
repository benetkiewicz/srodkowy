using FluentAssertions;
using Srodkowy.Functions.Services;

namespace Srodkowy.Functions.Tests;

public sealed class UrlNormalizerTests
{
    [Fact]
    public void Normalize_should_strip_query_and_fragment()
    {
        var normalized = UrlNormalizer.Normalize("https://example.com/story/123/?utm_source=test#section");

        normalized.Should().Be("https://example.com/story/123");
    }

    [Fact]
    public void IsCandidateArticleUrl_should_reject_non_article_paths()
    {
        var isCandidate = UrlNormalizer.IsCandidateArticleUrl("https://oko.press", "https://oko.press/tag/polityka");

        isCandidate.Should().BeFalse();
    }

    [Fact]
    public void IsCandidateArticleUrl_should_accept_same_domain_article_paths()
    {
        var isCandidate = UrlNormalizer.IsCandidateArticleUrl("https://onet.pl", "https://wiadomosci.onet.pl/kraj/testowy-artykul/abcd123");

        isCandidate.Should().BeTrue();
    }
}
