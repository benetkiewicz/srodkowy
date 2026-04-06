using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Srodkowy.Functions.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceDiscoverySelectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscoveryExcludeTags",
                table: "Sources",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscoveryIncludeTags",
                table: "Sources",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("06f296d0-9a17-4446-a800-b9cd969aec68"),
                columns: new[] { "DiscoveryExcludeTags", "DiscoveryIncludeTags", "DiscoveryUrl" },
                values: new object[] { null, "[\"main\"]", "https://tvrepublika.pl/tag/Wydarzenia-dnia" });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("1b706c1f-08a6-44c3-9dbe-abdb3f0e1921"),
                columns: new[] { "DiscoveryExcludeTags", "DiscoveryIncludeTags" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("21236d5c-52b3-467b-8052-08f0018d245b"),
                columns: new[] { "DiscoveryExcludeTags", "DiscoveryIncludeTags" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("21ba4856-b5bb-4121-b812-be8e4f429914"),
                columns: new[] { "DiscoveryExcludeTags", "DiscoveryIncludeTags" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("21fcd750-a76d-418e-ace0-1ced0c5e2b39"),
                columns: new[] { "DiscoveryExcludeTags", "DiscoveryIncludeTags" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("36941f6c-8d76-45b5-81d9-fcfac1278bfc"),
                columns: new[] { "DiscoveryExcludeTags", "DiscoveryIncludeTags" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("422dead7-b6d9-468f-beaf-eb3419dd17f1"),
                columns: new[] { "DiscoveryExcludeTags", "DiscoveryIncludeTags" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("4e673eb2-00b7-4b8d-a89b-451e19b04174"),
                columns: new[] { "DiscoveryExcludeTags", "DiscoveryIncludeTags" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("4fbdb458-fa8b-40ae-b177-f566ebca757f"),
                columns: new[] { "DiscoveryExcludeTags", "DiscoveryIncludeTags" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("6d5017af-2316-4c54-8079-80b8988f6ca5"),
                columns: new[] { "DiscoveryExcludeTags", "DiscoveryIncludeTags" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("71eb6457-b3a4-4545-9c63-134b709beae7"),
                columns: new[] { "DiscoveryExcludeTags", "DiscoveryIncludeTags" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("8934e6e3-3cea-4717-8782-d2996d19ffb8"),
                columns: new[] { "DiscoveryExcludeTags", "DiscoveryIncludeTags" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("925c5ed1-cfd0-4090-9c18-5ef0b24264bc"),
                columns: new[] { "DiscoveryExcludeTags", "DiscoveryIncludeTags" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("a3daa853-f462-4953-8cf0-1f26c9b8b1c3"),
                columns: new[] { "DiscoveryExcludeTags", "DiscoveryIncludeTags" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("c1f7c30a-3680-49f8-9763-72e97bc5bf1b"),
                columns: new[] { "DiscoveryExcludeTags", "DiscoveryIncludeTags" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("c57f4d5d-405f-4336-9156-8a5bf388c889"),
                columns: new[] { "DiscoveryExcludeTags", "DiscoveryIncludeTags" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("db3f3b4c-27b7-4ac3-8a21-f94d5c74447c"),
                columns: new[] { "DiscoveryExcludeTags", "DiscoveryIncludeTags" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("dbcd8890-2997-4016-af5f-8d244e2d980c"),
                columns: new[] { "DiscoveryExcludeTags", "DiscoveryIncludeTags" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("ed1ccaf0-2bf3-4faf-a660-80dba33908bd"),
                columns: new[] { "DiscoveryExcludeTags", "DiscoveryIncludeTags" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscoveryExcludeTags",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "DiscoveryIncludeTags",
                table: "Sources");

            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: new Guid("06f296d0-9a17-4446-a800-b9cd969aec68"),
                column: "DiscoveryUrl",
                value: "https://tvrepublika.pl");
        }
    }
}
