using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoyalIdentity.Storage.EntityFramework.Sqlite.OperationalMigrations
{
    /// <inheritdoc />
    public partial class InitialOperational : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "authorize_parameters",
                columns: table => new
                {
                    realm_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    handle_digest = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    payload_version = table.Column<int>(type: "INTEGER", nullable: false),
                    protected_payload = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authorize_parameters", x => new { x.realm_id, x.handle_digest });
                });

            migrationBuilder.CreateTable(
                name: "consents",
                columns: table => new
                {
                    realm_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    subject_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    client_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    payload_version = table.Column<int>(type: "INTEGER", nullable: false),
                    protected_payload = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consents", x => new { x.realm_id, x.subject_id, x.client_id });
                });

            migrationBuilder.CreateTable(
                name: "protocol_artifacts",
                columns: table => new
                {
                    realm_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    artifact_type = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    lookup_digest = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    subject_id = table.Column<string>(type: "TEXT", nullable: true, collation: "BINARY"),
                    client_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    session_id = table.Column<string>(type: "TEXT", nullable: true, collation: "BINARY"),
                    redirect_uri = table.Column<string>(type: "TEXT", nullable: true, collation: "BINARY"),
                    access_token_type = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    consumed_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    state_version = table.Column<int>(type: "INTEGER", nullable: false),
                    claims_mode = table.Column<int>(type: "INTEGER", nullable: true),
                    payload_version = table.Column<int>(type: "INTEGER", nullable: true),
                    protected_payload = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_protocol_artifacts", x => new { x.realm_id, x.artifact_type, x.lookup_digest });
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    realm_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    session_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    subject_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    authentication_method = table.Column<string>(type: "TEXT", nullable: false),
                    identity_provider = table.Column<string>(type: "TEXT", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_seen_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ended_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    security_stamp = table.Column<string>(type: "TEXT", nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => new { x.realm_id, x.session_id });
                });

            migrationBuilder.CreateTable(
                name: "user_session_clients",
                columns: table => new
                {
                    realm_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    session_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    client_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    first_seen_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_seen_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_session_clients", x => new { x.realm_id, x.session_id, x.client_id });
                    table.ForeignKey(
                        name: "FK_user_session_clients_user_sessions_realm_id_session_id",
                        columns: x => new { x.realm_id, x.session_id },
                        principalTable: "user_sessions",
                        principalColumns: new[] { "realm_id", "session_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_authorize_parameters_expiration",
                table: "authorize_parameters",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_consents_expiration",
                table: "consents",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_protocol_artifacts_consumed",
                table: "protocol_artifacts",
                columns: new[] { "artifact_type", "consumed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_protocol_artifacts_expiration",
                table: "protocol_artifacts",
                columns: new[] { "artifact_type", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_protocol_artifacts_session",
                table: "protocol_artifacts",
                columns: new[] { "realm_id", "session_id" });

            migrationBuilder.CreateIndex(
                name: "ix_protocol_artifacts_subject",
                table: "protocol_artifacts",
                columns: new[] { "realm_id", "artifact_type", "subject_id", "client_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_ended",
                table: "user_sessions",
                column: "ended_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_expiration",
                table: "user_sessions",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_subject",
                table: "user_sessions",
                columns: new[] { "realm_id", "subject_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authorize_parameters");

            migrationBuilder.DropTable(
                name: "consents");

            migrationBuilder.DropTable(
                name: "protocol_artifacts");

            migrationBuilder.DropTable(
                name: "user_session_clients");

            migrationBuilder.DropTable(
                name: "user_sessions");
        }
    }
}
