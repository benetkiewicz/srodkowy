using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Srodkowy.Functions.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSourceDiscoveryTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @RemovedSourceIds TABLE (Id uniqueidentifier PRIMARY KEY);
                INSERT INTO @RemovedSourceIds (Id)
                VALUES
                    ('1b706c1f-08a6-44c3-9dbe-abdb3f0e1921'),
                    ('21236d5c-52b3-467b-8052-08f0018d245b'),
                    ('4e673eb2-00b7-4b8d-a89b-451e19b04174'),
                    ('925c5ed1-cfd0-4090-9c18-5ef0b24264bc'),
                    ('a3daa853-f462-4953-8cf0-1f26c9b8b1c3'),
                    ('c1f7c30a-3680-49f8-9763-72e97bc5bf1b'),
                    ('ed1ccaf0-2bf3-4faf-a660-80dba33908bd');

                DELETE cca
                FROM [CandidateClusterArticles] AS cca
                INNER JOIN [Articles] AS a ON a.[Id] = cca.[ArticleId]
                WHERE a.[SourceId] IN (SELECT Id FROM @RemovedSourceIds);

                DELETE cc
                FROM [CandidateClusters] AS cc
                INNER JOIN [Articles] AS a ON a.[Id] = cc.[RepresentativeArticleId]
                WHERE a.[SourceId] IN (SELECT Id FROM @RemovedSourceIds);

                DELETE cr
                FROM [ClusterRuns] AS cr
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [CandidateClusters] AS cc
                    WHERE cc.[ClusterRunId] = cr.[Id]);
                """);

            migrationBuilder.DeleteData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("1b706c1f-08a6-44c3-9dbe-abdb3f0e1921"));

            migrationBuilder.DeleteData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("21236d5c-52b3-467b-8052-08f0018d245b"));

            migrationBuilder.DeleteData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("4e673eb2-00b7-4b8d-a89b-451e19b04174"));

            migrationBuilder.DeleteData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("925c5ed1-cfd0-4090-9c18-5ef0b24264bc"));

            migrationBuilder.DeleteData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("a3daa853-f462-4953-8cf0-1f26c9b8b1c3"));

            migrationBuilder.DeleteData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("c1f7c30a-3680-49f8-9763-72e97bc5bf1b"));

            migrationBuilder.DeleteData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("ed1ccaf0-2bf3-4faf-a660-80dba33908bd"));

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("21ba4856-b5bb-4121-b812-be8e4f429914"),
                columns: new[] { "DiscoveryIncludeTags", "DiscoveryUrl" },
                values: new object[] { "[\"main\"]", "https://tvn24.pl/najnowsze" });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("21fcd750-a76d-418e-ace0-1ced0c5e2b39"),
                column: "DiscoveryUrl",
                value: "https://wiadomosci.gazeta.pl/wiadomosci/");

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("36941f6c-8d76-45b5-81d9-fcfac1278bfc"),
                column: "DiscoveryIncludeTags",
                value: "[\".wrapper\"]");

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("422dead7-b6d9-468f-beaf-eb3419dd17f1"),
                columns: new[] { "DiscoveryIncludeTags", "DiscoveryUrl" },
                values: new object[] { "[\"main\"]", "https://tysol.pl/wiadomosci" });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("4fbdb458-fa8b-40ae-b177-f566ebca757f"),
                columns: new[] { "DiscoveryIncludeTags", "DiscoveryUrl" },
                values: new object[] { "[\"aside\"]", "https://www.radiomaryja.pl/informacje" });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("6d5017af-2316-4c54-8079-80b8988f6ca5"),
                column: "DiscoveryUrl",
                value: "https://oko.press/temat/polityka-zagraniczna");

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("71eb6457-b3a4-4545-9c63-134b709beae7"),
                column: "DiscoveryIncludeTags",
                value: "[\"main\"]");

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("8934e6e3-3cea-4717-8782-d2996d19ffb8"),
                columns: new[] { "DiscoveryIncludeTags", "DiscoveryUrl" },
                values: new object[] { "[\"section\"]", "https://niezalezna.pl/najnowsze" });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("c57f4d5d-405f-4336-9156-8a5bf388c889"),
                column: "DiscoveryUrl",
                value: "https://www.prawy.pl/najnowsze");

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("db3f3b4c-27b7-4ac3-8a21-f94d5c74447c"),
                columns: new[] { "DiscoveryIncludeTags", "DiscoveryUrl" },
                values: new object[] { "[\"main\"]", "https://wpolityce.pl/najnowsze" });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("dbcd8890-2997-4016-af5f-8d244e2d980c"),
                columns: new[] { "DiscoveryIncludeTags", "DiscoveryUrl" },
                values: new object[] { "[\"main\"]", "https://wiadomosci.onet.pl/" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("21ba4856-b5bb-4121-b812-be8e4f429914"),
                columns: new[] { "DiscoveryIncludeTags", "DiscoveryUrl" },
                values: new object[] { null, "https://tvn24.pl" });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("21fcd750-a76d-418e-ace0-1ced0c5e2b39"),
                column: "DiscoveryUrl",
                value: "https://wiadomosci.gazeta.pl");

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("36941f6c-8d76-45b5-81d9-fcfac1278bfc"),
                column: "DiscoveryIncludeTags",
                value: null);

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("422dead7-b6d9-468f-beaf-eb3419dd17f1"),
                columns: new[] { "DiscoveryIncludeTags", "DiscoveryUrl" },
                values: new object[] { null, "https://www.tysol.pl" });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("4fbdb458-fa8b-40ae-b177-f566ebca757f"),
                columns: new[] { "DiscoveryIncludeTags", "DiscoveryUrl" },
                values: new object[] { null, "https://www.radiomaryja.pl" });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("6d5017af-2316-4c54-8079-80b8988f6ca5"),
                column: "DiscoveryUrl",
                value: "https://oko.press");

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("71eb6457-b3a4-4545-9c63-134b709beae7"),
                column: "DiscoveryIncludeTags",
                value: null);

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("8934e6e3-3cea-4717-8782-d2996d19ffb8"),
                columns: new[] { "DiscoveryIncludeTags", "DiscoveryUrl" },
                values: new object[] { null, "https://niezalezna.pl" });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("c57f4d5d-405f-4336-9156-8a5bf388c889"),
                column: "DiscoveryUrl",
                value: "https://prawy.pl");

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("db3f3b4c-27b7-4ac3-8a21-f94d5c74447c"),
                columns: new[] { "DiscoveryIncludeTags", "DiscoveryUrl" },
                values: new object[] { null, "https://wpolityce.pl" });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("dbcd8890-2997-4016-af5f-8d244e2d980c"),
                columns: new[] { "DiscoveryIncludeTags", "DiscoveryUrl" },
                values: new object[] { null, "https://www.onet.pl" });

            migrationBuilder.InsertData(
                table: "Sources",
                columns: new[] { "Id", "Active", "BaseUrl", "Camp", "DiscoveryExcludeTags", "DiscoveryIncludeTags", "DiscoveryUrl", "Name" },
                values: new object[,]
                {
                    { new Guid("1b706c1f-08a6-44c3-9dbe-abdb3f0e1921"), true, "https://strajk.eu", "left", null, null, "https://strajk.eu", "Strajk.eu" },
                    { new Guid("21236d5c-52b3-467b-8052-08f0018d245b"), true, "https://trybuna.info", "left", null, null, "https://trybuna.info", "Trybuna.info" },
                    { new Guid("4e673eb2-00b7-4b8d-a89b-451e19b04174"), true, "https://polityka.pl", "left", null, null, "https://www.polityka.pl", "Polityka.pl" },
                    { new Guid("925c5ed1-cfd0-4090-9c18-5ef0b24264bc"), true, "https://newsweek.pl", "left", null, null, "https://www.newsweek.pl", "Newsweek.pl" },
                    { new Guid("a3daa853-f462-4953-8cf0-1f26c9b8b1c3"), true, "https://krytykapolityczna.pl", "left", null, null, "https://krytykapolityczna.pl", "Krytykapolityczna.pl" },
                    { new Guid("c1f7c30a-3680-49f8-9763-72e97bc5bf1b"), true, "https://pch24.pl", "right", null, null, "https://pch24.pl", "PCh24.pl" },
                    { new Guid("ed1ccaf0-2bf3-4faf-a660-80dba33908bd"), true, "https://natemat.pl", "left", null, null, "https://natemat.pl", "Natemat.pl" }
                });
        }
    }
}
