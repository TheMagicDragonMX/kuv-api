using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace kuv_api.Migrations
{
    /// <inheritdoc />
    public partial class UserBioAndPhotoFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A real rename, so the bios people already wrote are kept
            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Users",
                newName: "Bio");

            // Profile pictures move out of the database into files (uploads/pfp). A migration cannot write files, so
            // the pictures that were stored in this column are discarded: those users choose their picture again.
            migrationBuilder.DropColumn(
                name: "Photo",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The pictures that were files stay in uploads/pfp; the column comes back empty
            migrationBuilder.AddColumn<string>(
                name: "Photo",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.RenameColumn(
                name: "Bio",
                table: "Users",
                newName: "Description");
        }
    }
}
