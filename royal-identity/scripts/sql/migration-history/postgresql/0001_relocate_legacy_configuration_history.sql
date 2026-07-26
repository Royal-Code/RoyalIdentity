-- Relocates a Plano 2 PostgreSQL database from EF's default public."__EFMigrationsHistory" to the
-- Configuration history of plan-data-operational-storage DF23: configuration."__EFMigrationsHistory".
--
-- Run this BEFORE any migration command. EF must never consult the new location while the old one still holds
-- the applied ids: it would read an empty history and try to recreate every table.
--
-- It is the SQL equivalent of PostgreSqlMigrationsHistoryBootstrap: idempotent, preserving every migration id
-- verbatim, and failing closed when both histories exist — that ambiguity is not something to resolve by
-- merging or dropping either side silently.

DO $$
DECLARE
    legacy_exists boolean;
    target_exists boolean;
BEGIN
    SELECT EXISTS(
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = '__EFMigrationsHistory'
    ) INTO legacy_exists;

    SELECT EXISTS(
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'configuration' AND table_name = '__EFMigrationsHistory'
    ) INTO target_exists;

    IF legacy_exists AND target_exists THEN
        RAISE EXCEPTION
            'Both public."__EFMigrationsHistory" and configuration."__EFMigrationsHistory" exist. The '
            'migrations history is ambiguous and will not be merged or dropped automatically; resolve it '
            'manually before migrating.';
    END IF;

    IF legacy_exists THEN
        CREATE SCHEMA IF NOT EXISTS configuration;
        ALTER TABLE public."__EFMigrationsHistory" SET SCHEMA configuration;
    END IF;
END $$;
