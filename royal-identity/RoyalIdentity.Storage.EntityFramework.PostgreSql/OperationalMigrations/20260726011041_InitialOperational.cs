using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoyalIdentity.Storage.EntityFramework.PostgreSql.OperationalMigrations
{
    /// <inheritdoc />
    public partial class InitialOperational : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "operation");

            migrationBuilder.CreateTable(
                name: "authorize_parameters",
                schema: "operation",
                columns: table => new
                {
                    realm_id = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    handle_digest = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    payload_version = table.Column<int>(type: "integer", nullable: false),
                    protected_payload = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authorize_parameters", x => new { x.realm_id, x.handle_digest });
                });

            migrationBuilder.CreateTable(
                name: "consents",
                schema: "operation",
                columns: table => new
                {
                    realm_id = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    subject_id = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    client_id = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    payload_version = table.Column<int>(type: "integer", nullable: false),
                    protected_payload = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consents", x => new { x.realm_id, x.subject_id, x.client_id });
                });

            migrationBuilder.CreateTable(
                name: "protocol_artifacts",
                schema: "operation",
                columns: table => new
                {
                    realm_id = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    artifact_type = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    lookup_digest = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    subject_id = table.Column<string>(type: "text", nullable: true, collation: "C"),
                    client_id = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    session_id = table.Column<string>(type: "text", nullable: true, collation: "C"),
                    redirect_uri = table.Column<string>(type: "text", nullable: true, collation: "C"),
                    access_token_type = table.Column<int>(type: "integer", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    state_version = table.Column<int>(type: "integer", nullable: false),
                    claims_mode = table.Column<int>(type: "integer", nullable: true),
                    payload_version = table.Column<int>(type: "integer", nullable: true),
                    protected_payload = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_protocol_artifacts", x => new { x.realm_id, x.artifact_type, x.lookup_digest });
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                schema: "operation",
                columns: table => new
                {
                    realm_id = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    session_id = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    subject_id = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    authentication_method = table.Column<string>(type: "text", nullable: false),
                    identity_provider = table.Column<string>(type: "text", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ended_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => new { x.realm_id, x.session_id });
                });

            migrationBuilder.CreateTable(
                name: "user_session_clients",
                schema: "operation",
                columns: table => new
                {
                    realm_id = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    session_id = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    client_id = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    first_seen_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_session_clients", x => new { x.realm_id, x.session_id, x.client_id });
                    table.ForeignKey(
                        name: "FK_user_session_clients_user_sessions_realm_id_session_id",
                        columns: x => new { x.realm_id, x.session_id },
                        principalSchema: "operation",
                        principalTable: "user_sessions",
                        principalColumns: new[] { "realm_id", "session_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_authorize_parameters_expiration",
                schema: "operation",
                table: "authorize_parameters",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_consents_expiration",
                schema: "operation",
                table: "consents",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_protocol_artifacts_consumed",
                schema: "operation",
                table: "protocol_artifacts",
                columns: new[] { "artifact_type", "consumed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_protocol_artifacts_expiration",
                schema: "operation",
                table: "protocol_artifacts",
                columns: new[] { "artifact_type", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_protocol_artifacts_session",
                schema: "operation",
                table: "protocol_artifacts",
                columns: new[] { "realm_id", "session_id" });

            migrationBuilder.CreateIndex(
                name: "ix_protocol_artifacts_subject",
                schema: "operation",
                table: "protocol_artifacts",
                columns: new[] { "realm_id", "artifact_type", "subject_id", "client_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_ended",
                schema: "operation",
                table: "user_sessions",
                column: "ended_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_expiration",
                schema: "operation",
                table: "user_sessions",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_subject",
                schema: "operation",
                table: "user_sessions",
                columns: new[] { "realm_id", "subject_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authorize_parameters",
                schema: "operation");

            migrationBuilder.DropTable(
                name: "consents",
                schema: "operation");

            migrationBuilder.DropTable(
                name: "protocol_artifacts",
                schema: "operation");

            migrationBuilder.DropTable(
                name: "user_session_clients",
                schema: "operation");

            migrationBuilder.DropTable(
                name: "user_sessions",
                schema: "operation");
        }
    }
}
