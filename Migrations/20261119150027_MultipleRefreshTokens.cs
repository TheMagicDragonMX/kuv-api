using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace kuv_api.Migrations
{
    /// <inheritdoc />
    public partial class MultipleRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            // The session each user has now moves to the new table, so nobody is signed out by the change. The token
            // itself is not stored (only its hash), so its expiry cannot be read back: it gets a full lifetime.
            migrationBuilder.Sql("""
                INSERT INTO "RefreshTokens" ("UserId", "TokenHash", "ExpiresAt", "CreatedAt")
                SELECT "Id", "RefreshToken", now() + interval '7 days', now()
                FROM "Users"
                WHERE "RefreshToken" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "RefreshToken",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefreshToken",
                table: "Users",
                type: "text",
                nullable: true);

            // Only one session per user fits back: the newest one stays signed in
            migrationBuilder.Sql("""
                UPDATE "Users" SET "RefreshToken" = newest."TokenHash"
                FROM (
                    SELECT DISTINCT ON ("UserId") "UserId", "TokenHash"
                    FROM "RefreshTokens"
                    ORDER BY "UserId", "CreatedAt" DESC
                ) AS newest
                WHERE newest."UserId" = "Users"."Id";
                """);

            migrationBuilder.DropTable(
                name: "RefreshTokens");
        }
    }
}
