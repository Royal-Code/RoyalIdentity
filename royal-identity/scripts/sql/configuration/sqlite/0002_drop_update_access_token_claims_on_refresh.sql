BEGIN TRANSACTION;
CREATE TABLE "ef_temp_clients" (
    "realm_id" TEXT COLLATE BINARY NOT NULL,
    "client_id" TEXT COLLATE BINARY NOT NULL,
    "absolute_refresh_token_lifetime" INTEGER NOT NULL,
    "access_token_lifetime" INTEGER NOT NULL,
    "allow_all_resource_servers" INTEGER NOT NULL,
    "allow_logout_without_user_confirmation" INTEGER NOT NULL,
    "allow_offline_access" INTEGER NOT NULL,
    "allow_plain_text_pkce" INTEGER NOT NULL,
    "allow_remember_consent" INTEGER NOT NULL,
    "always_include_user_claims_in_id_token" INTEGER NOT NULL,
    "always_send_client_claims" INTEGER NOT NULL,
    "authorization_code_lifetime" INTEGER NOT NULL,
    "back_channel_logout_session_required" INTEGER NOT NULL,
    "client_claims_prefix" TEXT NULL,
    "client_type" INTEGER NOT NULL,
    "client_uri" TEXT NULL,
    "consent_lifetime" INTEGER NULL,
    "description" TEXT NULL,
    "enable_local_login" INTEGER NOT NULL,
    "enabled" INTEGER NOT NULL,
    "front_channel_logout_session_required" INTEGER NOT NULL,
    "identity_token_lifetime" INTEGER NOT NULL,
    "include_jwt_id" INTEGER NOT NULL,
    "logo_uri" TEXT NULL,
    "name" TEXT NOT NULL,
    "protocol_type" TEXT NOT NULL,
    "refresh_token_expiration" INTEGER NOT NULL,
    "refresh_token_post_consumed_time_tolerance_ticks" INTEGER NOT NULL,
    "require_client_secret" INTEGER NOT NULL,
    "require_consent" INTEGER NOT NULL,
    "require_pkce" INTEGER NOT NULL,
    "sliding_refresh_token_lifetime" INTEGER NOT NULL,
    "user_sso_lifetime" INTEGER NULL,
    CONSTRAINT "PK_clients" PRIMARY KEY ("realm_id", "client_id"),
    CONSTRAINT "FK_clients_realms_realm_id" FOREIGN KEY ("realm_id") REFERENCES "realms" ("id") ON DELETE RESTRICT
);

INSERT INTO "ef_temp_clients" ("realm_id", "client_id", "absolute_refresh_token_lifetime", "access_token_lifetime", "allow_all_resource_servers", "allow_logout_without_user_confirmation", "allow_offline_access", "allow_plain_text_pkce", "allow_remember_consent", "always_include_user_claims_in_id_token", "always_send_client_claims", "authorization_code_lifetime", "back_channel_logout_session_required", "client_claims_prefix", "client_type", "client_uri", "consent_lifetime", "description", "enable_local_login", "enabled", "front_channel_logout_session_required", "identity_token_lifetime", "include_jwt_id", "logo_uri", "name", "protocol_type", "refresh_token_expiration", "refresh_token_post_consumed_time_tolerance_ticks", "require_client_secret", "require_consent", "require_pkce", "sliding_refresh_token_lifetime", "user_sso_lifetime")
SELECT "realm_id", "client_id", "absolute_refresh_token_lifetime", "access_token_lifetime", "allow_all_resource_servers", "allow_logout_without_user_confirmation", "allow_offline_access", "allow_plain_text_pkce", "allow_remember_consent", "always_include_user_claims_in_id_token", "always_send_client_claims", "authorization_code_lifetime", "back_channel_logout_session_required", "client_claims_prefix", "client_type", "client_uri", "consent_lifetime", "description", "enable_local_login", "enabled", "front_channel_logout_session_required", "identity_token_lifetime", "include_jwt_id", "logo_uri", "name", "protocol_type", "refresh_token_expiration", "refresh_token_post_consumed_time_tolerance_ticks", "require_client_secret", "require_consent", "require_pkce", "sliding_refresh_token_lifetime", "user_sso_lifetime"
FROM "clients";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "clients";

ALTER TABLE "ef_temp_clients" RENAME TO "clients";

COMMIT;

PRAGMA foreign_keys = 1;

INSERT INTO "__ConfigurationMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260725171141_DropUpdateAccessTokenClaimsOnRefresh', '10.0.10');

