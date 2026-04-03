using FluentAssertions;
using Srodkowy.Functions.Services;

namespace Srodkowy.Functions.Tests;

public sealed class ArticleContentConverterTests
{
    [Fact]
    public void ToPlainText_should_strip_basic_markdown_formatting()
    {
        const string markdown = "# Tytul\n\nTo jest [link](https://example.com) i **pogrubienie**.";

        var plainText = ArticleContentConverter.ToPlainText(markdown);

        plainText.Should().Be("Tytul To jest link i pogrubienie.");
    }

    [Fact]
    public void ExtractTitle_should_return_first_non_empty_line()
    {
        const string markdown = "\n\n# Tytul artykulu\n\nDalsza tresc";

        var title = ArticleContentConverter.ExtractTitle(markdown);

        title.Should().Be("Tytul artykulu");
    }
}
