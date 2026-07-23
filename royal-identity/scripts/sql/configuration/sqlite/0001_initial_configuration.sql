CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;
CREATE TABLE "realms" (
    "id" TEXT COLLATE BINARY NOT NULL CONSTRAINT "PK_realms" PRIMARY KEY,
    "path" TEXT COLLATE BINARY NOT NULL,
    "domain" TEXT COLLATE BINARY NOT NULL,
    "display_name" TEXT NOT NULL,
    "enabled" INTEGER NOT NULL,
    "internal" INTEGER NOT NULL,
    "options_version" INTEGER NOT NULL,
    "options_json" TEXT NOT NULL,
    "deleted_at_utc" TEXT NULL
);

CREATE TABLE "server_options" (
    "id" INTEGER NOT NULL CONSTRAINT "PK_server_options" PRIMARY KEY,
    "payload_version" INTEGER NOT NULL,
    "payload_json" TEXT NOT NULL,
    "updated_at_utc" TEXT NOT NULL,
    CONSTRAINT "ck_server_options_singleton" CHECK (id = 1)
);

CREATE TABLE "clients" (
    "realm_id" TEXT COLLATE BINARY NOT NULL,
    "client_id" TEXT COLLATE BINARY NOT NULL,
    "name" TEXT NOT NULL,
    "description" TEXT NULL,
    "client_uri" TEXT NULL,
    "logo_uri" TEXT NULL,
    "enabled" INTEGER NOT NULL,
    "protocol_type" TEXT NOT NULL,
    "require_pkce" INTEGER NOT NULL,
    "allow_plain_text_pkce" INTEGER NOT NULL,
    "client_type" INTEGER NOT NULL,
    "allow_offline_access" INTEGER NOT NULL,
    "allow_all_resource_servers" INTEGER NOT NULL,
    "include_jwt_id" INTEGER NOT NULL,
    "always_send_client_claims" INTEGER NOT NULL,
    "always_include_user_claims_in_id_token" INTEGER NOT NULL,
    "client_claims_prefix" TEXT NULL,
    "enable_local_login" INTEGER NOT NULL,
    "user_sso_lifetime" INTEGER NULL,
    "access_token_lifetime" INTEGER NOT NULL,
    "identity_token_lifetime" INTEGER NOT NULL,
    "authorization_code_lifetime" INTEGER NOT NULL,
    "absolute_refresh_token_lifetime" INTEGER NOT NULL,
    "sliding_refresh_token_lifetime" INTEGER NOT NULL,
    "consent_lifetime" INTEGER NULL,
    "require_consent" INTEGER NOT NULL,
    "allow_remember_consent" INTEGER NOT NULL,
    "require_client_secret" INTEGER NOT NULL,
    "refresh_token_expiration" INTEGER NOT NULL,
    "refresh_token_post_consumed_time_tolerance_ticks" INTEGER NOT NULL,
    "update_access_token_claims_on_refresh" INTEGER NOT NULL,
    "allow_logout_without_user_confirmation" INTEGER NOT NULL,
    "front_channel_logout_session_required" INTEGER NOT NULL,
    "back_channel_logout_session_required" INTEGER NOT NULL,
    CONSTRAINT "PK_clients" PRIMARY KEY ("realm_id", "client_id"),
    CONSTRAINT "FK_clients_realms_realm_id" FOREIGN KEY ("realm_id") REFERENCES "realms" ("id") ON DELETE RESTRICT
);

CREATE TABLE "signing_keys" (
    "realm_id" TEXT COLLATE BINARY NOT NULL,
    "key_id" TEXT COLLATE BINARY NOT NULL,
    "name" TEXT NOT NULL,
    "security_algorithm" TEXT NOT NULL,
    "serialization_format" INTEGER NOT NULL,
    "encoding" INTEGER NOT NULL,
    "created_utc" TEXT NOT NULL,
    "not_before_utc" TEXT NULL,
    "expires_utc" TEXT NULL,
    "protector_id" TEXT NOT NULL,
    "protected_material" TEXT NOT NULL,
    CONSTRAINT "PK_signing_keys" PRIMARY KEY ("realm_id", "key_id"),
    CONSTRAINT "FK_signing_keys_realms_realm_id" FOREIGN KEY ("realm_id") REFERENCES "realms" ("id") ON DELETE RESTRICT
);

CREATE TABLE "client_claims" (
    "realm_id" TEXT COLLATE BINARY NOT NULL,
    "client_id" TEXT COLLATE BINARY NOT NULL,
    "ordinal" INTEGER NOT NULL,
    "type" TEXT NOT NULL,
    "value" TEXT NOT NULL,
    "value_type" TEXT NULL,
    "issuer" TEXT NULL,
    "original_issuer" TEXT NULL,
    CONSTRAINT "PK_client_claims" PRIMARY KEY ("realm_id", "client_id", "ordinal"),
    CONSTRAINT "FK_client_claims_clients_realm_id_client_id" FOREIGN KEY ("realm_id", "client_id") REFERENCES "clients" ("realm_id", "client_id") ON DELETE CASCADE
);

CREATE TABLE "client_secrets" (
    "realm_id" TEXT COLLATE BINARY NOT NULL,
    "client_id" TEXT COLLATE BINARY NOT NULL,
    "ordinal" INTEGER NOT NULL,
    "type" TEXT NOT NULL,
    "value" TEXT NOT NULL,
    "description" TEXT NULL,
    "expiration_utc" TEXT NULL,
    CONSTRAINT "PK_client_secrets" PRIMARY KEY ("realm_id", "client_id", "ordinal"),
    CONSTRAINT "FK_client_secrets_clients_realm_id_client_id" FOREIGN KEY ("realm_id", "client_id") REFERENCES "clients" ("realm_id", "client_id") ON DELETE CASCADE
);

CREATE TABLE "client_string_values" (
    "realm_id" TEXT COLLATE BINARY NOT NULL,
    "client_id" TEXT COLLATE BINARY NOT NULL,
    "kind" TEXT COLLATE BINARY NOT NULL,
    "comparison_key" TEXT COLLATE BINARY NOT NULL,
    "value" TEXT NOT NULL,
    CONSTRAINT "PK_client_string_values" PRIMARY KEY ("realm_id", "client_id", "kind", "comparison_key"),
    CONSTRAINT "FK_client_string_values_clients_realm_id_client_id" FOREIGN KEY ("realm_id", "client_id") REFERENCES "clients" ("realm_id", "client_id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "ux_realms_domain" ON "realms" ("domain");

CREATE UNIQUE INDEX "ux_realms_path" ON "realms" ("path");

CREATE INDEX "ix_signing_keys_realm_created" ON "signing_keys" ("realm_id", "created_utc");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260722164339_InitialConfiguration', '10.0.10');

COMMIT;

