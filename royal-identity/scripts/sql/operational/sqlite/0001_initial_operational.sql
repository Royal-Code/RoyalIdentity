CREATE TABLE IF NOT EXISTS "__OperationalMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___OperationalMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;
CREATE TABLE "authorize_parameters" (
    "realm_id" TEXT COLLATE BINARY NOT NULL,
    "handle_digest" TEXT COLLATE BINARY NOT NULL,
    "created_at_utc" TEXT NOT NULL,
    "expires_at_utc" TEXT NOT NULL,
    "payload_version" INTEGER NOT NULL,
    "protected_payload" TEXT NOT NULL,
    CONSTRAINT "PK_authorize_parameters" PRIMARY KEY ("realm_id", "handle_digest")
);

CREATE TABLE "consents" (
    "realm_id" TEXT COLLATE BINARY NOT NULL,
    "subject_id" TEXT COLLATE BINARY NOT NULL,
    "client_id" TEXT COLLATE BINARY NOT NULL,
    "created_at_utc" TEXT NOT NULL,
    "expires_at_utc" TEXT NULL,
    "payload_version" INTEGER NOT NULL,
    "protected_payload" TEXT NOT NULL,
    CONSTRAINT "PK_consents" PRIMARY KEY ("realm_id", "subject_id", "client_id")
);

CREATE TABLE "protocol_artifacts" (
    "realm_id" TEXT COLLATE BINARY NOT NULL,
    "artifact_type" TEXT COLLATE BINARY NOT NULL,
    "lookup_digest" TEXT COLLATE BINARY NOT NULL,
    "subject_id" TEXT COLLATE BINARY NULL,
    "client_id" TEXT COLLATE BINARY NOT NULL,
    "session_id" TEXT COLLATE BINARY NULL,
    "redirect_uri" TEXT COLLATE BINARY NULL,
    "access_token_type" INTEGER NULL,
    "created_at_utc" TEXT NOT NULL,
    "expires_at_utc" TEXT NOT NULL,
    "consumed_at_utc" TEXT NULL,
    "state_version" INTEGER NOT NULL,
    "claims_mode" INTEGER NULL,
    "payload_version" INTEGER NULL,
    "protected_payload" TEXT NULL,
    CONSTRAINT "PK_protocol_artifacts" PRIMARY KEY ("realm_id", "artifact_type", "lookup_digest")
);

CREATE TABLE "user_sessions" (
    "realm_id" TEXT COLLATE BINARY NOT NULL,
    "session_id" TEXT COLLATE BINARY NOT NULL,
    "subject_id" TEXT COLLATE BINARY NOT NULL,
    "authentication_method" TEXT NOT NULL,
    "identity_provider" TEXT NOT NULL,
    "started_at_utc" TEXT NOT NULL,
    "last_seen_at_utc" TEXT NOT NULL,
    "expires_at_utc" TEXT NULL,
    "ended_at_utc" TEXT NULL,
    "security_stamp" TEXT NULL,
    "is_active" INTEGER NOT NULL,
    CONSTRAINT "PK_user_sessions" PRIMARY KEY ("realm_id", "session_id")
);

CREATE TABLE "user_session_clients" (
    "realm_id" TEXT COLLATE BINARY NOT NULL,
    "session_id" TEXT COLLATE BINARY NOT NULL,
    "client_id" TEXT COLLATE BINARY NOT NULL,
    "first_seen_at_utc" TEXT NOT NULL,
    "last_seen_at_utc" TEXT NOT NULL,
    CONSTRAINT "PK_user_session_clients" PRIMARY KEY ("realm_id", "session_id", "client_id"),
    CONSTRAINT "FK_user_session_clients_user_sessions_realm_id_session_id" FOREIGN KEY ("realm_id", "session_id") REFERENCES "user_sessions" ("realm_id", "session_id") ON DELETE CASCADE
);

CREATE INDEX "ix_authorize_parameters_expiration" ON "authorize_parameters" ("realm_id", "expires_at_utc");

CREATE INDEX "ix_consents_expiration" ON "consents" ("expires_at_utc");

CREATE INDEX "ix_protocol_artifacts_consumed" ON "protocol_artifacts" ("artifact_type", "consumed_at_utc");

CREATE INDEX "ix_protocol_artifacts_expiration" ON "protocol_artifacts" ("artifact_type", "expires_at_utc");

CREATE INDEX "ix_protocol_artifacts_session" ON "protocol_artifacts" ("realm_id", "session_id");

CREATE INDEX "ix_protocol_artifacts_subject" ON "protocol_artifacts" ("realm_id", "artifact_type", "subject_id", "client_id");

CREATE INDEX "ix_user_sessions_ended" ON "user_sessions" ("ended_at_utc");

CREATE INDEX "ix_user_sessions_expiration" ON "user_sessions" ("expires_at_utc");

CREATE INDEX "ix_user_sessions_subject" ON "user_sessions" ("realm_id", "subject_id", "is_active");

INSERT INTO "__OperationalMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260725030157_InitialOperational', '10.0.10');

COMMIT;

