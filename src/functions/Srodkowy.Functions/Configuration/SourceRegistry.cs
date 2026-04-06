namespace Srodkowy.Functions.Configuration;

public static class SourceRegistry
{
    public static IReadOnlyList<SourceDefinition> All { get; } =
    [
        new(
            Guid.Parse("dbcd8890-2997-4016-af5f-8d244e2d980c"),
            "Onet.pl",
            "https://onet.pl",
            "https://wiadomosci.onet.pl/",
            SourceCamp.Left,
            DiscoveryIncludeTags: ["main"]),
        new(
            Guid.Parse("21fcd750-a76d-418e-ace0-1ced0c5e2b39"),
            "Gazeta.pl",
            "https://gazeta.pl",
            "https://wiadomosci.gazeta.pl/wiadomosci/",
            SourceCamp.Left),
        new(
            Guid.Parse("6d5017af-2316-4c54-8079-80b8988f6ca5"),
            "Oko.press",
            "https://oko.press",
            "https://oko.press/temat/polityka-zagraniczna",
            SourceCamp.Left),
        new(
            Guid.Parse("21ba4856-b5bb-4121-b812-be8e4f429914"),
            "Tvn24.pl",
            "https://tvn24.pl",
            "https://tvn24.pl/najnowsze",
            SourceCamp.Left,
            DiscoveryIncludeTags: ["main"]),
        new(
            Guid.Parse("71eb6457-b3a4-4545-9c63-134b709beae7"),
            "Tokfm.pl",
            "https://tokfm.pl",
            "https://www.tokfm.pl",
            SourceCamp.Left,
            DiscoveryIncludeTags: ["main"]),
        new(
            Guid.Parse("8934e6e3-3cea-4717-8782-d2996d19ffb8"),
            "Niezalezna.pl",
            "https://niezalezna.pl",
            "https://niezalezna.pl/najnowsze",
            SourceCamp.Right,
            DiscoveryIncludeTags: ["section"]),
        new(
            Guid.Parse("db3f3b4c-27b7-4ac3-8a21-f94d5c74447c"),
            "Wpolityce.pl",
            "https://wpolityce.pl",
            "https://wpolityce.pl/najnowsze",
            SourceCamp.Right,
            DiscoveryIncludeTags: ["main"]),
        new(
            Guid.Parse("36941f6c-8d76-45b5-81d9-fcfac1278bfc"),
            "Dorzeczy.pl",
            "https://dorzeczy.pl",
            "https://dorzeczy.pl",
            SourceCamp.Right,
            DiscoveryIncludeTags: [".wrapper"]),
        new(
            Guid.Parse("c57f4d5d-405f-4336-9156-8a5bf388c889"),
            "Prawy.pl",
            "https://prawy.pl",
            "https://www.prawy.pl/najnowsze",
            SourceCamp.Right),
        new(
            Guid.Parse("06f296d0-9a17-4446-a800-b9cd969aec68"),
            "Tvrepublika.pl",
            "https://tvrepublika.pl",
            "https://tvrepublika.pl/tag/Wydarzenia-dnia",
            SourceCamp.Right,
            DiscoveryIncludeTags: ["main"]),
        new(
            Guid.Parse("4fbdb458-fa8b-40ae-b177-f566ebca757f"),
            "Radiomaryja.pl",
            "https://radiomaryja.pl",
            "https://www.radiomaryja.pl/informacje",
            SourceCamp.Right,
            DiscoveryIncludeTags: ["aside"]),
        new(
            Guid.Parse("422dead7-b6d9-468f-beaf-eb3419dd17f1"),
            "Tysol.pl",
            "https://tysol.pl",
            "https://tysol.pl/wiadomosci",
            SourceCamp.Right,
            DiscoveryIncludeTags: ["main"])
    ];
}
