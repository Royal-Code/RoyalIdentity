using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoyalIdentity.Storage.EntityFramework.PostgreSql.OperationalMigrations
{
    /// <inheritdoc />
    public partial class AddReplayHandles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "replay_handles",
                schema: "operation",
                columns: table => new
                {
                    realm_id = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    issuer = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    purpose = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    handle_digest = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_replay_handles", x => new { x.realm_id, x.issuer, x.purpose, x.handle_digest });
                });

            migrationBuilder.CreateIndex(
                name: "ix_replay_handles_expiration",
                schema: "operation",
                table: "replay_handles",
                column: "expires_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "replay_handles",
                schema: "operation");
        }
    }
}
