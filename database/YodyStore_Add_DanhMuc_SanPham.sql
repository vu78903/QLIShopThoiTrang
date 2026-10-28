BEGIN;

ALTER TABLE sanpham
    ADD COLUMN IF NOT EXISTS danhmuc varchar(20) NULL;

-- Chỉ tự gán những dòng chắc chắn là phụ kiện. Áo/quần cũ cần chọn Nam hoặc Nữ trên form.
UPDATE sanpham sp
SET danhmuc = 'PHU_KIEN'
FROM loaisanpham l
WHERE sp.maloai = l.maloai
  AND sp.danhmuc IS NULL
  AND (lower(l.tenloai) LIKE '%phụ kiện%' OR lower(l.tenloai) LIKE '%phu kien%');

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_sanpham_danhmuc') THEN
        ALTER TABLE sanpham
            ADD CONSTRAINT ck_sanpham_danhmuc
            CHECK (danhmuc IS NULL OR danhmuc IN ('NAM', 'NU', 'PHU_KIEN'));
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_sanpham_danhmuc ON sanpham(danhmuc);

COMMIT;
