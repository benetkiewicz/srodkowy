using System;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Srodkowy.Functions.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArticlePreparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CleanedAt",
                table: "Articles",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CleanedContentText",
                table: "Articles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CleanupError",
                table: "Articles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CleanupFlagsJson",
                table: "Articles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "CleanupInputHash",
                table: "Articles",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CleanupProcessor",
                table: "Articles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CleanupRunId",
                table: "Articles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CleanupStartedAt",
                table: "Articles",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CleanupStatus",
                table: "Articles",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmbeddedAt",
                table: "Articles",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<SqlVector<float>>(
                name: "Embedding",
                table: "Articles",
                type: "vector(1536)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingError",
                table: "Articles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingModel",
                table: "Articles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmbeddingRunId",
                table: "Articles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmbeddingStartedAt",
                table: "Articles",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingStatus",
                table: "Articles",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingTextHash",
                table: "Articles",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsProbablyNonArticle",
                table: "Articles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsReview",
                table: "Articles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "QualityScore",
                table: "Articles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Articles_CleanupRunId",
                table: "Articles",
                column: "CleanupRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_CleanupStatus",
                table: "Articles",
                column: "CleanupStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_CleanupStatus_ScrapedAt",
                table: "Articles",
                columns: new[] { "CleanupStatus", "ScrapedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Articles_EmbeddingRunId",
                table: "Articles",
                column: "EmbeddingRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_EmbeddingStatus",
                table: "Articles",
                column: "EmbeddingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_EmbeddingStatus_ScrapedAt",
                table: "Articles",
                columns: new[] { "EmbeddingStatus", "ScrapedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Articles_CleanupRunId",
                table: "Articles");

            migrationBuilder.DropIndex(
                name: "IX_Articles_CleanupStatus",
                table: "Articles");

            migrationBuilder.DropIndex(
                name: "IX_Articles_CleanupStatus_ScrapedAt",
                table: "Articles");

            migrationBuilder.DropIndex(
                name: "IX_Articles_EmbeddingRunId",
                table: "Articles");

            migrationBuilder.DropIndex(
                name: "IX_Articles_EmbeddingStatus",
                table: "Articles");

            migrationBuilder.DropIndex(
                name: "IX_Articles_EmbeddingStatus_ScrapedAt",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "CleanedAt",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "CleanedContentText",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "CleanupError",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "CleanupFlagsJson",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "CleanupInputHash",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "CleanupProcessor",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "CleanupRunId",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "CleanupStartedAt",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "CleanupStatus",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "EmbeddedAt",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "EmbeddingError",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "EmbeddingModel",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "EmbeddingRunId",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "EmbeddingStartedAt",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "EmbeddingStatus",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "EmbeddingTextHash",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "IsProbablyNonArticle",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "NeedsReview",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "QualityScore",
                table: "Articles");
        }
    }
}
