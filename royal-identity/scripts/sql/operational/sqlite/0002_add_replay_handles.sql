BEGIN TRANSACTION;
CREATE TABLE "replay_handles" (
    "realm_id" TEXT COLLATE BINARY NOT NULL,
    "issuer" TEXT COLLATE BINARY NOT NULL,
    "purpose" TEXT COLLATE BINARY NOT NULL,
    "handle_digest" TEXT COLLATE BINARY NOT NULL,
    "expires_at_utc" TEXT NOT NULL,
    CONSTRAINT "PK_replay_handles" PRIMARY KEY ("realm_id", "issuer", "purpose", "handle_digest")
);

CREATE INDEX "ix_replay_handles_expiration" ON "replay_handles" ("expires_at_utc");

INSERT INTO "__OperationalMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260730012818_AddReplayHandles', '10.0.10');

COMMIT;

