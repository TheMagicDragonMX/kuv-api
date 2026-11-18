using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace kuv_api.Migrations
{
    /// <inheritdoc />
    public partial class CreationHasVideo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasVideo",
                table: "Creations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Creations that had a video keep it, before the column that said so is dropped
            migrationBuilder.Sql("UPDATE \"Creations\" SET \"HasVideo\" = TRUE WHERE \"VideoFileName\" IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "Creations");

            migrationBuilder.DropColumn(
                name: "VideoFileName",
                table: "Creations");

            migrationBuilder.DropColumn(
                name: "VideoMimeType",
                table: "Creations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasVideo",
                table: "Creations");

            migrationBuilder.AddColumn<double>(
                name: "DurationSeconds",
                table: "Creations",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "VideoFileName",
                table: "Creations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoMimeType",
                table: "Creations",
                type: "text",
                nullable: true);
        }
    }
}
