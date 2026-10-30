-- Apply AFTER Supabase-Customer-Cart.sql. Review before applying to the shared database.
-- Retain purchase snapshots even if Windows deletes a pending COD invoice.
-- Does not change the partner's stock, payment or loyalty workflow.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
DO $$
BEGIN
    IF to_regclass('webstore.cart') IS NULL THEN
        RAISE EXCEPTION 'Apply Supabase-Customer-Cart.sql first';
    END IF;
    -- Fail before enabling orders if the shared invoice schema has not been migrated.
    PERFORM sohd,nguondon,tennguoinhan,ghichugiaohang,manv,trangthaigiaohang,trangthaithanhtoan FROM public.hoadon LIMIT 0;
    PERFORM sohd,masku,soluong,dongia,thanhtien FROM public.chitiethoadon LIMIT 0;
END $$;
CREATE TABLE IF NOT EXISTS webstore.order_snapshot (
    sohd varchar(30) PRIMARY KEY,
    makh varchar(20) NOT NULL REFERENCES public.khachhang(makh) ON DELETE RESTRICT,
    ngaylap timestamp without time zone NOT NULL,
    recipient varchar(150) NOT NULL, phone varchar(20) NOT NULL,
    address varchar(300) NOT NULL, note varchar(500) NOT NULL DEFAULT '',
    total numeric(18,2) NOT NULL CHECK(total>0)
);
CREATE INDEX IF NOT EXISTS ix_web_order_customer ON webstore.order_snapshot(makh,ngaylap DESC);
CREATE TABLE IF NOT EXISTS webstore.order_line_snapshot (
    sohd varchar(30) NOT NULL REFERENCES webstore.order_snapshot(sohd) ON DELETE RESTRICT,
    masku varchar(40) NOT NULL, name varchar(200) NOT NULL,
    size varchar(30) NOT NULL, color varchar(60) NOT NULL,
    quantity integer NOT NULL CHECK(quantity>0), price numeric(18,2) NOT NULL CHECK(price>0),
    PRIMARY KEY(sohd,masku)
);
ALTER TABLE webstore.order_snapshot ENABLE ROW LEVEL SECURITY;
ALTER TABLE webstore.order_line_snapshot ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON webstore.order_snapshot,webstore.order_line_snapshot FROM PUBLIC;
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='anon') THEN
        REVOKE ALL ON webstore.order_snapshot,webstore.order_line_snapshot FROM anon;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='authenticated') THEN
        REVOKE ALL ON webstore.order_snapshot,webstore.order_line_snapshot FROM authenticated;
    END IF;
END $$;
COMMIT;
