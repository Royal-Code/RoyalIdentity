START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operation."__EFMigrationsHistory" WHERE "MigrationId" = '20260730012828_AddReplayHandles') THEN
    CREATE TABLE operation.replay_handles (
        realm_id text COLLATE "C" NOT NULL,
        issuer text COLLATE "C" NOT NULL,
        purpose text COLLATE "C" NOT NULL,
        handle_digest text COLLATE "C" NOT NULL,
        expires_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT "PK_replay_handles" PRIMARY KEY (realm_id, issuer, purpose, handle_digest)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operation."__EFMigrationsHistory" WHERE "MigrationId" = '20260730012828_AddReplayHandles') THEN
    CREATE INDEX ix_replay_handles_expiration ON operation.replay_handles (expires_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM operation."__EFMigrationsHistory" WHERE "MigrationId" = '20260730012828_AddReplayHandles') THEN
    INSERT INTO operation."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260730012828_AddReplayHandles', '10.0.10');
    END IF;
END $EF$;
COMMIT;

