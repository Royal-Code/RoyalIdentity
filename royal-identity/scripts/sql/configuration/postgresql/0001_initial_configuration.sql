CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260722233806_InitialConfiguration') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'configuration') THEN
            CREATE SCHEMA configuration;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260722233806_InitialConfiguration') THEN
    CREATE TABLE configuration.realms (
        id text COLLATE "C" NOT NULL,
        path text COLLATE "C" NOT NULL,
        domain text COLLATE "C" NOT NULL,
        display_name text NOT NULL,
        enabled boolean NOT NULL,
        internal boolean NOT NULL,
        options_version integer NOT NULL,
        options_json jsonb NOT NULL,
        deleted_at_utc timestamp with time zone,
        CONSTRAINT "PK_realms" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260722233806_InitialConfiguration') THEN
    CREATE TABLE configuration.server_options (
        id smallint NOT NULL,
        payload_version integer NOT NULL,
        payload_json jsonb NOT NULL,
        updated_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT "PK_server_options" PRIMARY KEY (id),
        CONSTRAINT ck_server_options_singleton CHECK (id = 1)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260722233806_InitialConfiguration') THEN
    CREATE TABLE configuration.clients (
        realm_id text COLLATE "C" NOT NULL,
        client_id text COLLATE "C" NOT NULL,
        name text NOT NULL,
        description text,
        client_uri text,
        logo_uri text,
        enabled boolean NOT NULL,
        protocol_type text NOT NULL,
        require_pkce boolean NOT NULL,
        allow_plain_text_pkce boolean NOT NULL,
        client_type integer NOT NULL,
        allow_offline_access boolean NOT NULL,
        allow_all_resource_servers boolean NOT NULL,
        include_jwt_id boolean NOT NULL,
        always_send_client_claims boolean NOT NULL,
        always_include_user_claims_in_id_token boolean NOT NULL,
        client_claims_prefix text,
        enable_local_login boolean NOT NULL,
        user_sso_lifetime integer,
        access_token_lifetime integer NOT NULL,
        identity_token_lifetime integer NOT NULL,
        authorization_code_lifetime integer NOT NULL,
        absolute_refresh_token_lifetime integer NOT NULL,
        sliding_refresh_token_lifetime integer NOT NULL,
        consent_lifetime integer,
        require_consent boolean NOT NULL,
        allow_remember_consent boolean NOT NULL,
        require_client_secret boolean NOT NULL,
        refresh_token_expiration integer NOT NULL,
        refresh_token_post_consumed_time_tolerance_ticks bigint NOT NULL,
        update_access_token_claims_on_refresh boolean NOT NULL,
        allow_logout_without_user_confirmation boolean NOT NULL,
        front_channel_logout_session_required boolean NOT NULL,
        back_channel_logout_session_required boolean NOT NULL,
        CONSTRAINT "PK_clients" PRIMARY KEY (realm_id, client_id),
        CONSTRAINT "FK_clients_realms_realm_id" FOREIGN KEY (realm_id) REFERENCES configuration.realms (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260722233806_InitialConfiguration') THEN
    CREATE TABLE configuration.signing_keys (
        realm_id text COLLATE "C" NOT NULL,
        key_id text COLLATE "C" NOT NULL,
        name text NOT NULL,
        security_algorithm text NOT NULL,
        serialization_format integer NOT NULL,
        encoding integer NOT NULL,
        created_utc timestamp with time zone NOT NULL,
        not_before_utc timestamp with time zone,
        expires_utc timestamp with time zone,
        protector_id text NOT NULL,
        protected_material text NOT NULL,
        CONSTRAINT "PK_signing_keys" PRIMARY KEY (realm_id, key_id),
        CONSTRAINT "FK_signing_keys_realms_realm_id" FOREIGN KEY (realm_id) REFERENCES configuration.realms (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260722233806_InitialConfiguration') THEN
    CREATE TABLE configuration.client_claims (
        realm_id text COLLATE "C" NOT NULL,
        client_id text COLLATE "C" NOT NULL,
        ordinal integer NOT NULL,
        type text NOT NULL,
        value text NOT NULL,
        value_type text,
        issuer text,
        original_issuer text,
        CONSTRAINT "PK_client_claims" PRIMARY KEY (realm_id, client_id, ordinal),
        CONSTRAINT "FK_client_claims_clients_realm_id_client_id" FOREIGN KEY (realm_id, client_id) REFERENCES configuration.clients (realm_id, client_id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260722233806_InitialConfiguration') THEN
    CREATE TABLE configuration.client_secrets (
        realm_id text COLLATE "C" NOT NULL,
        client_id text COLLATE "C" NOT NULL,
        ordinal integer NOT NULL,
        type text NOT NULL,
        value text NOT NULL,
        description text,
        expiration_utc timestamp with time zone,
        CONSTRAINT "PK_client_secrets" PRIMARY KEY (realm_id, client_id, ordinal),
        CONSTRAINT "FK_client_secrets_clients_realm_id_client_id" FOREIGN KEY (realm_id, client_id) REFERENCES configuration.clients (realm_id, client_id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260722233806_InitialConfiguration') THEN
    CREATE TABLE configuration.client_string_values (
        realm_id text COLLATE "C" NOT NULL,
        client_id text COLLATE "C" NOT NULL,
        kind text COLLATE "C" NOT NULL,
        comparison_key text COLLATE "C" NOT NULL,
        value text NOT NULL,
        CONSTRAINT "PK_client_string_values" PRIMARY KEY (realm_id, client_id, kind, comparison_key),
        CONSTRAINT "FK_client_string_values_clients_realm_id_client_id" FOREIGN KEY (realm_id, client_id) REFERENCES configuration.clients (realm_id, client_id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260722233806_InitialConfiguration') THEN
    CREATE UNIQUE INDEX ux_realms_domain ON configuration.realms (domain);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260722233806_InitialConfiguration') THEN
    CREATE UNIQUE INDEX ux_realms_path ON configuration.realms (path);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260722233806_InitialConfiguration') THEN
    CREATE INDEX ix_signing_keys_realm_created ON configuration.signing_keys (realm_id, created_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260722233806_InitialConfiguration') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260722233806_InitialConfiguration', '10.0.10');
    END IF;
END $EF$;
COMMIT;

