using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Srodkowy.Functions.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateClustering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClusterRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TriggeredBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LookbackHours = table.Column<int>(type: "int", nullable: false),
                    CandidateArticleCount = table.Column<int>(type: "int", nullable: false),
                    DeduplicatedArticleCount = table.Column<int>(type: "int", nullable: false),
                    ClusterCount = table.Column<int>(type: "int", nullable: false),
                    QualifiedClusterCount = table.Column<int>(type: "int", nullable: false),
                    ErrorSummary = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClusterRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CandidateClusters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClusterRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepresentativeArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    RankScore = table.Column<double>(type: "float", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ArticleCount = table.Column<int>(type: "int", nullable: false),
                    DistinctSourceCount = table.Column<int>(type: "int", nullable: false),
                    LeftArticleCount = table.Column<int>(type: "int", nullable: false),
                    RightArticleCount = table.Column<int>(type: "int", nullable: false),
                    WindowStartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WindowEndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MeanSimilarity = table.Column<double>(type: "float", nullable: false),
                    NarrativeDivergenceScore = table.Column<double>(type: "float", nullable: false),
                    BalanceScore = table.Column<double>(type: "float", nullable: false),
                    SelectionVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateClusters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateClusters_Articles_RepresentativeArticleId",
                        column: x => x.RepresentativeArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CandidateClusters_ClusterRuns_ClusterRunId",
                        column: x => x.ClusterRunId,
                        principalTable: "ClusterRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateClusterArticles",
                columns: table => new
                {
                    CandidateClusterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Camp = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SimilarityToRepresentative = table.Column<double>(type: "float", nullable: false),
                    IsRepresentative = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateClusterArticles", x => new { x.CandidateClusterId, x.ArticleId });
                    table.ForeignKey(
                        name: "FK_CandidateClusterArticles_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CandidateClusterArticles_CandidateClusters_CandidateClusterId",
                        column: x => x.CandidateClusterId,
                        principalTable: "CandidateClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateClusterArticles_ArticleId",
                table: "CandidateClusterArticles",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateClusters_ClusterRunId",
                table: "CandidateClusters",
                column: "ClusterRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateClusters_Rank",
                table: "CandidateClusters",
                column: "Rank");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateClusters_RepresentativeArticleId",
                table: "CandidateClusters",
                column: "RepresentativeArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateClusters_Status",
                table: "CandidateClusters",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ClusterRuns_StartedAt",
                table: "ClusterRuns",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateClusterArticles");

            migrationBuilder.DropTable(
                name: "CandidateClusters");

            migrationBuilder.DropTable(
                name: "ClusterRuns");
        }
    }
}
