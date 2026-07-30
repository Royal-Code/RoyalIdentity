using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoyalIdentity.Storage.EntityFramework.Sqlite.OperationalMigrations
{
    /// <inheritdoc />
    public partial class AddReplayHandles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "replay_handles",
                columns: table => new
                {
                    realm_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    issuer = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    purpose = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    handle_digest = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    expires_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_replay_handles", x => new { x.realm_id, x.issuer, x.purpose, x.handle_digest });
                });

            migrationBuilder.CreateIndex(
                name: "ix_replay_handles_expiration",
                table: "replay_handles",
                column: "expires_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "replay_handles");
        }
    }
}
