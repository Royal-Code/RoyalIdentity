using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoyalIdentity.Storage.EntityFramework.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "realms",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    path = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    domain = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    display_name = table.Column<string>(type: "TEXT", nullable: false),
                    enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    @internal = table.Column<bool>(name: "internal", type: "INTEGER", nullable: false),
                    options_version = table.Column<int>(type: "INTEGER", nullable: false),
                    options_json = table.Column<string>(type: "TEXT", nullable: false),
                    deleted_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_realms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "server_options",
                columns: table => new
                {
                    id = table.Column<short>(type: "INTEGER", nullable: false),
                    payload_version = table.Column<int>(type: "INTEGER", nullable: false),
                    payload_json = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_server_options", x => x.id);
                    table.CheckConstraint("ck_server_options_singleton", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    realm_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    client_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    client_uri = table.Column<string>(type: "TEXT", nullable: true),
                    logo_uri = table.Column<string>(type: "TEXT", nullable: true),
                    enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    protocol_type = table.Column<string>(type: "TEXT", nullable: false),
                    require_pkce = table.Column<bool>(type: "INTEGER", nullable: false),
                    allow_plain_text_pkce = table.Column<bool>(type: "INTEGER", nullable: false),
                    client_type = table.Column<int>(type: "INTEGER", nullable: false),
                    allow_offline_access = table.Column<bool>(type: "INTEGER", nullable: false),
                    allow_all_resource_servers = table.Column<bool>(type: "INTEGER", nullable: false),
                    include_jwt_id = table.Column<bool>(type: "INTEGER", nullable: false),
                    always_send_client_claims = table.Column<bool>(type: "INTEGER", nullable: false),
                    always_include_user_claims_in_id_token = table.Column<bool>(type: "INTEGER", nullable: false),
                    client_claims_prefix = table.Column<string>(type: "TEXT", nullable: true),
                    enable_local_login = table.Column<bool>(type: "INTEGER", nullable: false),
                    user_sso_lifetime = table.Column<int>(type: "INTEGER", nullable: true),
                    access_token_lifetime = table.Column<int>(type: "INTEGER", nullable: false),
                    identity_token_lifetime = table.Column<int>(type: "INTEGER", nullable: false),
                    authorization_code_lifetime = table.Column<int>(type: "INTEGER", nullable: false),
                    absolute_refresh_token_lifetime = table.Column<int>(type: "INTEGER", nullable: false),
                    sliding_refresh_token_lifetime = table.Column<int>(type: "INTEGER", nullable: false),
                    consent_lifetime = table.Column<int>(type: "INTEGER", nullable: true),
                    require_consent = table.Column<bool>(type: "INTEGER", nullable: false),
                    allow_remember_consent = table.Column<bool>(type: "INTEGER", nullable: false),
                    require_client_secret = table.Column<bool>(type: "INTEGER", nullable: false),
                    refresh_token_expiration = table.Column<int>(type: "INTEGER", nullable: false),
                    refresh_token_post_consumed_time_tolerance_ticks = table.Column<long>(type: "INTEGER", nullable: false),
                    update_access_token_claims_on_refresh = table.Column<bool>(type: "INTEGER", nullable: false),
                    allow_logout_without_user_confirmation = table.Column<bool>(type: "INTEGER", nullable: false),
                    front_channel_logout_session_required = table.Column<bool>(type: "INTEGER", nullable: false),
                    back_channel_logout_session_required = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => new { x.realm_id, x.client_id });
                    table.ForeignKey(
                        name: "FK_clients_realms_realm_id",
                        column: x => x.realm_id,
                        principalTable: "realms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "signing_keys",
                columns: table => new
                {
                    realm_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    key_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    security_algorithm = table.Column<string>(type: "TEXT", nullable: false),
                    serialization_format = table.Column<int>(type: "INTEGER", nullable: false),
                    encoding = table.Column<int>(type: "INTEGER", nullable: false),
                    created_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    not_before_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    expires_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    protector_id = table.Column<string>(type: "TEXT", nullable: false),
                    protected_material = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signing_keys", x => new { x.realm_id, x.key_id });
                    table.ForeignKey(
                        name: "FK_signing_keys_realms_realm_id",
                        column: x => x.realm_id,
                        principalTable: "realms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "client_claims",
                columns: table => new
                {
                    realm_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    client_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    type = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false),
                    value_type = table.Column<string>(type: "TEXT", nullable: true),
                    issuer = table.Column<string>(type: "TEXT", nullable: true),
                    original_issuer = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_claims", x => new { x.realm_id, x.client_id, x.ordinal });
                    table.ForeignKey(
                        name: "FK_client_claims_clients_realm_id_client_id",
                        columns: x => new { x.realm_id, x.client_id },
                        principalTable: "clients",
                        principalColumns: new[] { "realm_id", "client_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "client_secrets",
                columns: table => new
                {
                    realm_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    client_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    type = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    expiration_utc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_secrets", x => new { x.realm_id, x.client_id, x.ordinal });
                    table.ForeignKey(
                        name: "FK_client_secrets_clients_realm_id_client_id",
                        columns: x => new { x.realm_id, x.client_id },
                        principalTable: "clients",
                        principalColumns: new[] { "realm_id", "client_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "client_string_values",
                columns: table => new
                {
                    realm_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    client_id = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    kind = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    comparison_key = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_string_values", x => new { x.realm_id, x.client_id, x.kind, x.comparison_key });
                    table.ForeignKey(
                        name: "FK_client_string_values_clients_realm_id_client_id",
                        columns: x => new { x.realm_id, x.client_id },
                        principalTable: "clients",
                        principalColumns: new[] { "realm_id", "client_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_realms_domain",
                table: "realms",
                column: "domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_realms_path",
                table: "realms",
                column: "path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_signing_keys_realm_created",
                table: "signing_keys",
                columns: new[] { "realm_id", "created_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_claims");

            migrationBuilder.DropTable(
                name: "client_secrets");

            migrationBuilder.DropTable(
                name: "client_string_values");

            migrationBuilder.DropTable(
                name: "server_options");

            migrationBuilder.DropTable(
                name: "signing_keys");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "realms");
        }
    }
}
