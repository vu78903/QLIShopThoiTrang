BEGIN;

DO $$
DECLARE
    pk_name text;
BEGIN
    SELECT c.conname INTO pk_name
    FROM pg_constraint c
    JOIN pg_class t ON t.oid = c.conrelid
    JOIN pg_namespace n ON n.oid = t.relnamespace
    WHERE n.nspname = 'public'
      AND t.relname = 'sanpham'
      AND c.contype = 'p'
      AND pg_get_constraintdef(c.oid) NOT ILIKE '%(masku)%';

    IF pk_name IS NOT NULL THEN
        EXECUTE format('ALTER TABLE public.sanpham DROP CONSTRAINT %I', pk_name);
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint c
        JOIN pg_class t ON t.oid = c.conrelid
        WHERE t.relname = 'sanpham' AND c.contype = 'p'
    ) THEN
        ALTER TABLE public.sanpham
            ADD CONSTRAINT sanpham_pkey PRIMARY KEY (masku);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_sanpham_masp
    ON sanpham(masp);

CREATE UNIQUE INDEX IF NOT EXISTS ux_sanpham_bienthe
    ON sanpham(masp, lower(trim(mausac)), lower(trim(kichco)));

COMMIT;
