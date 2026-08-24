-- One-time migration: pre-modular schema (public.*) -> module schemas.
-- Run BEFORE starting the new application version against an existing database.
-- Identity columns are identical between layouts; only table locations change.
--
-- NOTE: verify column names against the legacy migration before running. EF maps
-- `uint Version` to the PostgreSQL xmin system column, so there is no physical
-- version column to copy — row versions come along automatically.

BEGIN;

-- Catalog
INSERT INTO catalog.products (id, name, price)
SELECT id, name, price FROM public.products
ON CONFLICT DO NOTHING;

-- Negotiations
INSERT INTO negotiations.customers (id, identity_user_id)
SELECT id, identity_user_id FROM public.customers
ON CONFLICT DO NOTHING;

INSERT INTO negotiations.negotiations
    (id, product_id, customer_id, base_price, current_offer, status,
     proposals_used, created_at_utc, last_proposal_at_utc, decided_at_utc)
SELECT id, product_id, customer_id, base_price, current_offer, status,
       proposals_used, created_at_utc, last_proposal_at_utc, decided_at_utc
FROM public.negotiations
ON CONFLICT DO NOTHING;

COMMIT;
