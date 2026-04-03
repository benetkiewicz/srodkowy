using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Srodkowy.Functions.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IngestionRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TriggeredBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceCount = table.Column<int>(type: "int", nullable: false),
                    DiscoveredLinkCount = table.Column<int>(type: "int", nullable: false),
                    CandidateLinkCount = table.Column<int>(type: "int", nullable: false),
                    ArticleCount = table.Column<int>(type: "int", nullable: false),
                    ErrorSummary = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestionRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DiscoveryUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Camp = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentMarkdown = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ScrapedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Articles_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Sources",
                columns: new[] { "Id", "Active", "BaseUrl", "Camp", "DiscoveryUrl", "Name" },
                values: new object[,]
                {
                    { new Guid("06f296d0-9a17-4446-a800-b9cd969aec68"), true, "https://tvrepublika.pl", "right", "https://tvrepublika.pl", "Tvrepublika.pl" },
                    { new Guid("1b706c1f-08a6-44c3-9dbe-abdb3f0e1921"), true, "https://strajk.eu", "left", "https://strajk.eu", "Strajk.eu" },
                    { new Guid("21236d5c-52b3-467b-8052-08f0018d245b"), true, "https://trybuna.info", "left", "https://trybuna.info", "Trybuna.info" },
                    { new Guid("21ba4856-b5bb-4121-b812-be8e4f429914"), true, "https://tvn24.pl", "left", "https://tvn24.pl", "Tvn24.pl" },
                    { new Guid("21fcd750-a76d-418e-ace0-1ced0c5e2b39"), true, "https://gazeta.pl", "left", "https://wiadomosci.gazeta.pl", "Gazeta.pl" },
                    { new Guid("36941f6c-8d76-45b5-81d9-fcfac1278bfc"), true, "https://dorzeczy.pl", "right", "https://dorzeczy.pl", "Dorzeczy.pl" },
                    { new Guid("422dead7-b6d9-468f-beaf-eb3419dd17f1"), true, "https://tysol.pl", "right", "https://www.tysol.pl", "Tysol.pl" },
                    { new Guid("4e673eb2-00b7-4b8d-a89b-451e19b04174"), true, "https://polityka.pl", "left", "https://www.polityka.pl", "Polityka.pl" },
                    { new Guid("4fbdb458-fa8b-40ae-b177-f566ebca757f"), true, "https://radiomaryja.pl", "right", "https://www.radiomaryja.pl", "Radiomaryja.pl" },
                    { new Guid("6d5017af-2316-4c54-8079-80b8988f6ca5"), true, "https://oko.press", "left", "https://oko.press", "Oko.press" },
                    { new Guid("71eb6457-b3a4-4545-9c63-134b709beae7"), true, "https://tokfm.pl", "left", "https://www.tokfm.pl", "Tokfm.pl" },
                    { new Guid("8934e6e3-3cea-4717-8782-d2996d19ffb8"), true, "https://niezalezna.pl", "right", "https://niezalezna.pl", "Niezalezna.pl" },
                    { new Guid("925c5ed1-cfd0-4090-9c18-5ef0b24264bc"), true, "https://newsweek.pl", "left", "https://www.newsweek.pl", "Newsweek.pl" },
                    { new Guid("a3daa853-f462-4953-8cf0-1f26c9b8b1c3"), true, "https://krytykapolityczna.pl", "left", "https://krytykapolityczna.pl", "Krytykapolityczna.pl" },
                    { new Guid("c1f7c30a-3680-49f8-9763-72e97bc5bf1b"), true, "https://pch24.pl", "right", "https://pch24.pl", "PCh24.pl" },
                    { new Guid("c57f4d5d-405f-4336-9156-8a5bf388c889"), true, "https://prawy.pl", "right", "https://prawy.pl", "Prawy.pl" },
                    { new Guid("db3f3b4c-27b7-4ac3-8a21-f94d5c74447c"), true, "https://wpolityce.pl", "right", "https://wpolityce.pl", "Wpolityce.pl" },
                    { new Guid("dbcd8890-2997-4016-af5f-8d244e2d980c"), true, "https://onet.pl", "left", "https://www.onet.pl", "Onet.pl" },
                    { new Guid("ed1ccaf0-2bf3-4faf-a660-80dba33908bd"), true, "https://natemat.pl", "left", "https://natemat.pl", "Natemat.pl" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Articles_ScrapedAt",
                table: "Articles",
                column: "ScrapedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_SourceId",
                table: "Articles",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Url",
                table: "Articles",
                column: "Url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngestionRuns_StartedAt",
                table: "IngestionRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_BaseUrl",
                table: "Sources",
                column: "BaseUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sources_Name",
                table: "Sources",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DropTable(
                name: "IngestionRuns");

            migrationBuilder.DropTable(
                name: "Sources");
        }
    }
}
