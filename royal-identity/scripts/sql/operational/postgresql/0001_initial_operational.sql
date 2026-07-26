DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'operation') THEN
        CREATE SCHEMA operation;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS operation."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operation."__EFMigrationsHistory" WHERE "MigrationId" = '20260726011041_InitialOperational') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'operation') THEN
            CREATE SCHEMA operation;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operation."__EFMigrationsHistory" WHERE "MigrationId" = '20260726011041_InitialOperational') THEN
    CREATE TABLE operation.authorize_parameters (
        realm_id text COLLATE "C" NOT NULL,
        handle_digest text COLLATE "C" NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        expires_at_utc timestamp with time zone NOT NULL,
        payload_version integer NOT NULL,
        protected_payload text NOT NULL,
        CONSTRAINT "PK_authorize_parameters" PRIMARY KEY (realm_id, handle_digest)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operation."__EFMigrationsHistory" WHERE "MigrationId" = '20260726011041_InitialOperational') THEN
    CREATE TABLE operation.consents (
        realm_id text COLLATE "C" NOT NULL,
        subject_id text COLLATE "C" NOT NULL,
        client_id text COLLATE "C" NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        expires_at_utc timestamp with time zone,
        payload_version integer NOT NULL,
        protected_payload text NOT NULL,
        CONSTRAINT "PK_consents" PRIMARY KEY (realm_id, subject_id, client_id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operation."__EFMigrationsHistory" WHERE "MigrationId" = '20260726011041_InitialOperational') THEN
    CREATE TABLE operation.protocol_artifacts (
        realm_id text COLLATE "C" NOT NULL,
        artifact_type text COLLATE "C" NOT NULL,
        lookup_digest text COLLATE "C" NOT NULL,
        subject_id text COLLATE "C",
        client_id text COLLATE "C" NOT NULL,
        session_id text COLLATE "C",
        redirect_uri text COLLATE "C",
        access_token_type integer,
        created_at_utc timestamp with time zone NOT NULL,
        expires_at_utc timestamp with time zone NOT NULL,
        consumed_at_utc timestamp with time zone,
        state_version integer NOT NULL,
        claims_mode integer,
        payload_version integer,
        protected_payload text,
        CONSTRAINT "PK_protocol_artifacts" PRIMARY KEY (realm_id, artifact_type, lookup_digest)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operation."__EFMigrationsHistory" WHERE "MigrationId" = '20260726011041_InitialOperational') THEN
    CREATE TABLE operation.user_sessions (
        realm_id text COLLATE "C" NOT NULL,
        session_id text COLLATE "C" NOT NULL,
        subject_id text COLLATE "C" NOT NULL,
        authentication_method text NOT NULL,
        identity_provider text NOT NULL,
        started_at_utc timestamp with time zone NOT NULL,
        last_seen_at_utc timestamp with time zone NOT NULL,
        expires_at_utc timestamp with time zone,
        ended_at_utc timestamp with time zone,
        security_stamp text,
        is_active boolean NOT NULL,
        CONSTRAINT "PK_user_sessions" PRIMARY KEY (realm_id, session_id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operation."__EFMigrationsHistory" WHERE "MigrationId" = '20260726011041_InitialOperational') THEN
    CREATE TABLE operation.user_session_clients (
        realm_id text COLLATE "C" NOT NULL,
        session_id text COLLATE "C" NOT NULL,
        client_id text COLLATE "C" NOT NULL,
        first_seen_at_utc timestamp with time zone NOT NULL,
        last_seen_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT "PK_user_session_clients" PRIMARY KEY (realm_id, session_id, client_id),
        CONSTRAINT "FK_user_session_clients_user_sessions_realm_id_session_id" FOREIGN KEY (realm_id, session_id) REFERENCES operation.user_sessions (realm_id, session_id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operation."__EFMigrationsHistory" WHERE "MigrationId" = '20260726011041_InitialOperational') THEN
    CREATE INDEX ix_authorize_parameters_expiration ON operation.authorize_parameters (expires_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operation."__EFMigrationsHistory" WHERE "MigrationId" = '20260726011041_InitialOperational') THEN
    CREATE INDEX ix_consents_expiration ON operation.consents (expires_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operation."__EFMigrationsHistory" WHERE "MigrationId" = '20260726011041_InitialOperational') THEN
    CREATE INDEX ix_protocol_artifacts_consumed ON operation.protocol_artifacts (artifact_type, consumed_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operation."__EFMigrationsHistory" WHERE "MigrationId" = '20260726011041_InitialOperational') THEN
    CREATE INDEX ix_protocol_artifacts_expiration ON operation.protocol_artifacts (artifact_type, expires_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operation."__EFMigrationsHistory" WHERE "MigrationId" = '20260726011041_InitialOperational') THEN
    CREATE INDEX ix_protocol_artifacts_session ON operation.protocol_artifacts (realm_id, session_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operation."__EFMigrationsHistory" WHERE "MigrationId" = '20260726011041_InitialOperational') THEN
    CREATE INDEX ix_protocol_artifacts_subject ON operation.protocol_artifacts (realm_id, artifact_type, subject_id, client_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operation."__EFMigrationsHistory" WHERE "MigrationId" = '20260726011041_InitialOperational') THEN
    CREATE INDEX ix_user_sessions_ended ON operation.user_sessions (ended_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operation."__EFMigrationsHistory" WHERE "MigrationId" = '20260726011041_InitialOperational') THEN
    CREATE INDEX ix_user_sessions_expiration ON operation.user_sessions (expires_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operation."__EFMigrationsHistory" WHERE "MigrationId" = '20260726011041_InitialOperational') THEN
    CREATE INDEX ix_user_sessions_subject ON operation.user_sessions (realm_id, subject_id, is_active);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operation."__EFMigrationsHistory" WHERE "MigrationId" = '20260726011041_InitialOperational') THEN
    INSERT INTO operation."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260726011041_InitialOperational', '10.0.10');
    END IF;
END $EF$;
COMMIT;

