-- One-time maintenance for upgraded persistent environments (compose/CI start fresh and
-- never need this script).
--
-- Run AFTER the first successful start of the new version against a database that was
-- migrated with docs/sql/legacy-data-migration.sql:
--   1. migration ids changed when contexts moved to module assemblies, so reset each
--      history table once before the first start;
--   2. drop the retired single-schema tables.

DELETE FROM "__EFMigrationsHistory_Identity";
DELETE FROM "__EFMigrationsHistory_Catalog";
DELETE FROM "__EFMigrationsHistory_Negotiations";

DROP TABLE IF EXISTS public.negotiations CASCADE;
DROP TABLE IF EXISTS public.customers CASCADE;
DROP TABLE IF EXISTS public.products CASCADE;
DROP TABLE IF EXISTS public.__efmigrations_history CASCADE;
