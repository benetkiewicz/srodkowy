using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Srodkowy.Functions.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoryPublishing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Editions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Building"),
                    Cycle = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClusterRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Editions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Editions_ClusterRuns_ClusterRunId",
                        column: x => x.ClusterRunId,
                        principalTable: "ClusterRuns",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Stories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EditionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateClusterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Headline = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Synthesis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MarkersJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stories_CandidateClusters_CandidateClusterId",
                        column: x => x.CandidateClusterId,
                        principalTable: "CandidateClusters",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Stories_Editions_EditionId",
                        column: x => x.EditionId,
                        principalTable: "Editions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoryArticles",
                columns: table => new
                {
                    StoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryArticles", x => new { x.StoryId, x.ArticleId });
                    table.ForeignKey(
                        name: "FK_StoryArticles_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StoryArticles_Stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "Stories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StorySides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Camp = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExcerptsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorySides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorySides_Stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "Stories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Editions_ClusterRunId",
                table: "Editions",
                column: "ClusterRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Editions_PublishedAt",
                table: "Editions",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Editions_Status",
                table: "Editions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Stories_CandidateClusterId",
                table: "Stories",
                column: "CandidateClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_Stories_EditionId",
                table: "Stories",
                column: "EditionId");

            migrationBuilder.CreateIndex(
                name: "IX_Stories_Rank",
                table: "Stories",
                column: "Rank");

            migrationBuilder.CreateIndex(
                name: "IX_StoryArticles_ArticleId",
                table: "StoryArticles",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_StorySides_StoryId",
                table: "StorySides",
                column: "StoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoryArticles");

            migrationBuilder.DropTable(
                name: "StorySides");

            migrationBuilder.DropTable(
                name: "Stories");

            migrationBuilder.DropTable(
                name: "Editions");
        }
    }
}
