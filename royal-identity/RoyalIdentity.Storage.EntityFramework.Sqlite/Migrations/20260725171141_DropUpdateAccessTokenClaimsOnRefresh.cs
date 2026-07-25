using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoyalIdentity.Storage.EntityFramework.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class DropUpdateAccessTokenClaimsOnRefresh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "update_access_token_claims_on_refresh",
                table: "clients");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "update_access_token_claims_on_refresh",
                table: "clients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
