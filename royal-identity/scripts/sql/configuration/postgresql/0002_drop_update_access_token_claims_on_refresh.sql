START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM configuration."__EFMigrationsHistory" WHERE "MigrationId" = '20260726011051_DropUpdateAccessTokenClaimsOnRefresh') THEN
    ALTER TABLE configuration.clients DROP COLUMN update_access_token_claims_on_refresh;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM configuration."__EFMigrationsHistory" WHERE "MigrationId" = '20260726011051_DropUpdateAccessTokenClaimsOnRefresh') THEN
    INSERT INTO configuration."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260726011051_DropUpdateAccessTokenClaimsOnRefresh', '10.0.10');
    END IF;
END $EF$;
COMMIT;

