-- Migrations history bootstrap for SQLite (plan-data-operational-storage DF23).
--
-- Moves a database migrated by Plano 2 off EF's default "__EFMigrationsHistory" and onto the Configuration
-- history name, so Configuration and Operational never share an evolution line when they share a file. This is
-- infrastructure, not a domain migration: run it BEFORE applying any migration with the new history configured,
-- otherwise EF reads an empty history and tries to recreate every table. The rename preserves the applied
-- migration ids verbatim.
--
-- SQLite has no conditional DDL, so this manual script is deliberately two explicit steps. The automated path
-- (SqliteMigrationsHistoryBootstrap, used by the runner) covers all four states — empty database, legacy only,
-- new only, and both — and is idempotent; prefer it when a runner is available.

-- Step 1 — inspect. Read the result before running step 2:
--   both rows        -> STOP. The history is ambiguous; resolve it manually. Never merge or drop either table:
--                       doing so silently reapplies or skips migrations.
--   legacy only      -> run step 2.
--   new only         -> nothing to do; the move already happened.
--   no rows          -> nothing to do; this database was never migrated.
SELECT name
FROM sqlite_master
WHERE type = 'table'
  AND name IN ('__EFMigrationsHistory', '__ConfigurationMigrationsHistory')
ORDER BY name;

-- Step 2 — apply, only in the "legacy only" case above.
-- ALTER TABLE "__EFMigrationsHistory" RENAME TO "__ConfigurationMigrationsHistory";
