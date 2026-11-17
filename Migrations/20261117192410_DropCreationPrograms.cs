using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace kuv_api.Migrations
{
    /// <inheritdoc />
    public partial class DropCreationPrograms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The creations saved before programs moved to files have nowhere to read their program from
            migrationBuilder.Sql("DELETE FROM \"Creations\";");

            migrationBuilder.DropColumn(
                name: "EnvironmentJson",
                table: "Creations");

            migrationBuilder.DropColumn(
                name: "ProgramJson",
                table: "Creations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnvironmentJson",
                table: "Creations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProgramJson",
                table: "Creations",
                type: "text",
                nullable: true);
        }
    }
}
