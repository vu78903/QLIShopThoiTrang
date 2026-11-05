-- Chỉ chạy trên database đã được chọn để triển khai. Ứng dụng không tự chạy script này.
BEGIN;
ALTER TABLE public.chitietdoitra
    ADD COLUMN IF NOT EXISTS soluongdakiemnhan integer DEFAULT 0,
    ADD COLUMN IF NOT EXISTS soluongnhaplai integer DEFAULT 0,
    ADD COLUMN IF NOT EXISTS nhatkykiemnhan jsonb DEFAULT '[]'::jsonb;

-- Giữ kết quả kiểm nhận cũ; tuyệt đối không cộng lại tồn khi chuyển cấu trúc.
UPDATE public.chitietdoitra SET
    soluongdakiemnhan=CASE WHEN trangthaikiemnhan IN ('DA_NHAP_LAI','KHONG_NHAP_LAI') THEN soluong ELSE COALESCE(soluongdakiemnhan,0) END,
    soluongnhaplai=CASE WHEN trangthaikiemnhan='DA_NHAP_LAI' THEN soluong ELSE COALESCE(soluongnhaplai,0) END,
    nhatkykiemnhan=COALESCE(nhatkykiemnhan,'[]'::jsonb);
ALTER TABLE public.chitietdoitra
    ALTER COLUMN soluongdakiemnhan SET DEFAULT 0, ALTER COLUMN soluongdakiemnhan SET NOT NULL,
    ALTER COLUMN soluongnhaplai SET DEFAULT 0, ALTER COLUMN soluongnhaplai SET NOT NULL,
    ALTER COLUMN nhatkykiemnhan SET DEFAULT '[]'::jsonb, ALTER COLUMN nhatkykiemnhan SET NOT NULL;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public.chitietdoitra'::regclass AND conname='ck_chitietdoitra_kiemnhan') THEN
        ALTER TABLE public.chitietdoitra ADD CONSTRAINT ck_chitietdoitra_kiemnhan CHECK (
            soluongdakiemnhan BETWEEN 0 AND soluong AND soluongnhaplai BETWEEN 0 AND soluongdakiemnhan
            AND jsonb_typeof(nhatkykiemnhan)='array');
    END IF;
END $$;
COMMIT;
