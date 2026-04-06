using FluentAssertions;
using Srodkowy.Functions.Configuration;

namespace Srodkowy.Functions.Tests;

public sealed class SourceRegistryTests
{
    [Fact]
    public void Registry_should_contain_expected_number_of_sources()
    {
        SourceRegistry.All.Should().HaveCount(12);
    }

    [Fact]
    public void Registry_should_keep_both_camps_represented()
    {
        SourceRegistry.All.Count(source => source.Camp == SourceCamp.Left).Should().Be(5);
        SourceRegistry.All.Count(source => source.Camp == SourceCamp.Right).Should().Be(7);
    }
}
