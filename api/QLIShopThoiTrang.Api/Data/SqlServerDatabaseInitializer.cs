namespace QLIShopThoiTrang.Api.Data;

// Kiểm tra database và các bảng bắt buộc trước khi API nhận yêu cầu.
public sealed class SqlServerDatabaseInitializer(SqlServerConnectionFactory db)
{
    public async Task InitializeAsync()
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        // PostgreSQL tự chuyển tên không đặt trong dấu ngoặc kép thành chữ thường.
        // Chuẩn hóa schema một lần để các câu SQL trong API không phụ thuộc kiểu viết hoa.
        await using (var normalize = connection.CreateCommand())
        {
            normalize.CommandText = """
                DO $$
                DECLARE r record;
                BEGIN
                  FOR r IN
                    SELECT table_name, column_name
                    FROM information_schema.columns
                    WHERE table_schema='public' AND column_name <> lower(column_name)
                  LOOP
                    EXECUTE format('ALTER TABLE public.%I RENAME COLUMN %I TO %I',
                                   r.table_name, r.column_name, lower(r.column_name));
                  END LOOP;
                  FOR r IN
                    SELECT table_name
                    FROM information_schema.tables
                    WHERE table_schema='public' AND table_type='BASE TABLE'
                      AND table_name <> lower(table_name)
                  LOOP
                    EXECUTE format('ALTER TABLE public.%I RENAME TO %I', r.table_name, lower(r.table_name));
                  END LOOP;
                END $$;
                """;
            await normalize.ExecuteNonQueryAsync();
        }
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema='public'
              AND table_name IN ('sanpham','nhanvien','khachhang','nhasanxuat','phieunhapkho')
            """;
        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        if (count < 5)
            throw new InvalidOperationException("Supabase chưa đủ bảng. Hãy chạy file YodyStore_Supabase_PostgreSQL.txt.");

        // Bổ sung thông tin dùng chung cho đơn giao từ POS và website.
        await using var deliveryColumns = connection.CreateCommand();
        deliveryColumns.CommandText = """
            ALTER TABLE hoadon ADD COLUMN IF NOT EXISTS tennguoinhan varchar(150) NULL;
            ALTER TABLE hoadon ADD COLUMN IF NOT EXISTS nguondon varchar(20) NOT NULL DEFAULT 'POS';
            ALTER TABLE hoadon ADD COLUMN IF NOT EXISTS ghichugiaohang varchar(500) NULL;
            CREATE INDEX IF NOT EXISTS ix_hoadon_giaohang
                ON hoadon (loaihoadon, trangthaigiaohang, ngaylap DESC);
            """;
        await deliveryColumns.ExecuteNonQueryAsync();

        await using var productKeys = connection.CreateCommand();
        productKeys.CommandText = """
            DO $$
            DECLARE pk_name text;
            BEGIN
              SELECT c.conname INTO pk_name
              FROM pg_constraint c
              JOIN pg_class t ON t.oid=c.conrelid
              JOIN pg_namespace n ON n.oid=t.relnamespace
              WHERE n.nspname='public' AND t.relname='sanpham' AND c.contype='p'
                AND pg_get_constraintdef(c.oid) NOT ILIKE '%(masku)%';
              IF pk_name IS NOT NULL THEN
                EXECUTE format('ALTER TABLE public.sanpham DROP CONSTRAINT %I', pk_name);
              END IF;
              IF NOT EXISTS (
                SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid
                WHERE t.relname='sanpham' AND c.contype='p'
              ) THEN
                ALTER TABLE public.sanpham ADD CONSTRAINT sanpham_pkey PRIMARY KEY (masku);
              END IF;
            END $$;
            CREATE INDEX IF NOT EXISTS ix_sanpham_masp ON sanpham(masp);
            CREATE UNIQUE INDEX IF NOT EXISTS ux_sanpham_bienthe
                ON sanpham(masp, lower(trim(mausac)), lower(trim(kichco)));
            """;
        await productKeys.ExecuteNonQueryAsync();

        // Danh mục hiển thị trên website tách biệt với loại sản phẩm (Áo, Quần...).
        // Dữ liệu cũ không được tự đoán Nam/Nữ; chỉ phụ kiện rõ ràng mới được gán tự động.
        await using var productAudience = connection.CreateCommand();
        productAudience.CommandText = """
            ALTER TABLE sanpham ADD COLUMN IF NOT EXISTS danhmuc varchar(20) NULL;
            UPDATE sanpham sp
            SET danhmuc='PHU_KIEN'
            FROM loaisanpham l
            WHERE sp.maloai=l.maloai AND sp.danhmuc IS NULL
              AND (lower(l.tenloai) LIKE '%phụ kiện%' OR lower(l.tenloai) LIKE '%phu kien%');
            DO $$
            BEGIN
              IF NOT EXISTS (
                SELECT 1 FROM pg_constraint WHERE conname='ck_sanpham_danhmuc'
              ) THEN
                ALTER TABLE sanpham ADD CONSTRAINT ck_sanpham_danhmuc
                  CHECK (danhmuc IS NULL OR danhmuc IN ('NAM','NU','PHU_KIEN'));
              END IF;
            END $$;
            CREATE INDEX IF NOT EXISTS ix_sanpham_danhmuc ON sanpham(danhmuc);
            """;
        await productAudience.ExecuteNonQueryAsync();

        // Mỗi sản phẩm chỉ nhập từ một nhà cung cấp. Phiếu nhập có thể chứa nhiều SKU,
        // nhưng tất cả SKU trên phiếu phải thuộc nhà cung cấp đã chọn.
        await using var productSupplier = connection.CreateCommand();
        productSupplier.CommandText = """
            ALTER TABLE sanpham ADD COLUMN IF NOT EXISTS mansx varchar(20) NULL;
            UPDATE sanpham sp
            SET mansx = CASE
                WHEN l.tenloai ILIKE '%quần tây%' OR l.tenloai ILIKE '%sơ mi%' OR l.tenloai ILIKE '%vest%'
                    THEN (SELECT n.mansx FROM nhasanxuat n WHERE n.tennsx ILIKE '%Việt Tiến%' LIMIT 1)
                WHEN l.tenloai ILIKE '%áo thun%' OR l.tenloai ILIKE '%baby tee%'
                    THEN (SELECT n.mansx FROM nhasanxuat n WHERE n.tennsx ILIKE '%An Đông%' LIMIT 1)
                WHEN l.tenloai ILIKE '%đồ lót%' OR l.tenloai ILIKE '%nón%' OR l.tenloai ILIKE '%vớ%'
                    THEN (SELECT n.mansx FROM nhasanxuat n WHERE n.tennsx ILIKE '%Thành Công%' LIMIT 1)
                WHEN sp.danhmuc='NAM'
                    THEN (SELECT n.mansx FROM nhasanxuat n WHERE n.tennsx ILIKE '%Dony%' LIMIT 1)
                WHEN sp.danhmuc='NU'
                    THEN (SELECT n.mansx FROM nhasanxuat n WHERE n.tennsx ILIKE '%Nhà Bè%' LIMIT 1)
                WHEN sp.danhmuc='PHU_KIEN'
                    THEN (SELECT n.mansx FROM nhasanxuat n WHERE n.tennsx ILIKE '%Thành Công%' LIMIT 1)
                ELSE sp.mansx
            END
            FROM loaisanpham l
            WHERE sp.maloai=l.maloai AND sp.mansx IS NULL;
            DO $$
            BEGIN
              IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_sanpham_nhasanxuat') THEN
                ALTER TABLE sanpham ADD CONSTRAINT fk_sanpham_nhasanxuat
                  FOREIGN KEY (mansx) REFERENCES nhasanxuat(mansx);
              END IF;
            END $$;
            CREATE INDEX IF NOT EXISTS ix_sanpham_mansx ON sanpham(mansx);
            """;
        await productSupplier.ExecuteNonQueryAsync();

        await using var importSupplierGuard = connection.CreateCommand();
        importSupplierGuard.CommandText = """
            CREATE OR REPLACE FUNCTION validate_import_product_supplier()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF NOT EXISTS (
                SELECT 1
                FROM phieunhapkho p
                JOIN sanpham s ON s.masku=NEW.masku
                WHERE p.sophieunk=NEW.sophieunk AND p.mansx=s.mansx
              ) THEN
                RAISE EXCEPTION 'Sản phẩm không thuộc nhà cung cấp của phiếu nhập kho.';
              END IF;
              RETURN NEW;
            END $$;
            DO $$
            BEGIN
              IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname='trg_chitietnhapkho_supplier') THEN
                CREATE TRIGGER trg_chitietnhapkho_supplier
                BEFORE INSERT OR UPDATE OF sophieunk,masku ON chitietnhapkho
                FOR EACH ROW EXECUTE FUNCTION validate_import_product_supplier();
              END IF;
            END $$;
            """;
        await importSupplierGuard.ExecuteNonQueryAsync();

        // Voucher cũ chỉ lưu ngày; chương trình sản phẩm cần lưu chính xác cả giờ và phút.
        await using var promotionTimeColumns = connection.CreateCommand();
        promotionTimeColumns.CommandText = """
            DROP VIEW IF EXISTS sanphamkhuyenmai_hientai;
            DO $$
            BEGIN
              IF EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='khuyenmai'
                  AND column_name IN ('ngaybatdau','ngayketthuc')
                  AND data_type <> 'timestamp without time zone'
              ) THEN
                ALTER TABLE khuyenmai
                    ALTER COLUMN ngaybatdau TYPE timestamp without time zone USING ngaybatdau::timestamp,
                    ALTER COLUMN ngayketthuc TYPE timestamp without time zone USING ngayketthuc::timestamp;
              END IF;
            END $$;
            """;
        await promotionTimeColumns.ExecuteNonQueryAsync();

        // Một chương trình có thể áp dụng cho nhiều MaSKU để giảm riêng đúng màu/size được chọn.
        await using var promotionProducts = connection.CreateCommand();
        promotionProducts.CommandText = """
            CREATE TABLE IF NOT EXISTS khuyenmaisanpham (
                makm varchar(30) NOT NULL,
                masku varchar(80) NOT NULL,
                phantramgiam numeric(5,2) NOT NULL,
                CONSTRAINT pk_khuyenmaisanpham PRIMARY KEY (makm, masku),
                CONSTRAINT fk_khuyenmaisanpham_khuyenmai
                    FOREIGN KEY (makm) REFERENCES khuyenmai(makm) ON DELETE CASCADE,
                CONSTRAINT fk_khuyenmaisanpham_sanpham
                    FOREIGN KEY (masku) REFERENCES sanpham(masku) ON DELETE CASCADE,
                CONSTRAINT ck_khuyenmaisanpham_phantram
                    CHECK (phantramgiam > 0 AND phantramgiam <= 100)
            );
            DROP VIEW IF EXISTS sanphamkhuyenmai_hientai;
            DO $$
            BEGIN
              IF EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='khuyenmaisanpham' AND column_name='masp'
              ) AND NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='khuyenmaisanpham' AND column_name='masku'
              ) THEN
                CREATE TEMP TABLE kmsp_chuyen_doi ON COMMIT DROP AS
                  SELECT old.makm, sp.masku, old.phantramgiam
                  FROM khuyenmaisanpham old JOIN sanpham sp ON sp.masp=old.masp;
                TRUNCATE khuyenmaisanpham;
                ALTER TABLE khuyenmaisanpham RENAME COLUMN masp TO masku;
                INSERT INTO khuyenmaisanpham(makm,masku,phantramgiam)
                  SELECT makm,masku,phantramgiam FROM kmsp_chuyen_doi ON CONFLICT DO NOTHING;
              END IF;
            END $$;
            DO $$
            BEGIN
              IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_khuyenmaisanpham_sanpham') THEN
                ALTER TABLE khuyenmaisanpham ADD CONSTRAINT fk_khuyenmaisanpham_sanpham
                  FOREIGN KEY (masku) REFERENCES sanpham(masku) ON DELETE CASCADE;
              END IF;
            END $$;
            DROP INDEX IF EXISTS ix_khuyenmaisanpham_masp;
            CREATE INDEX IF NOT EXISTS ix_khuyenmaisanpham_masku
                ON khuyenmaisanpham(masku);

            UPDATE khuyenmai km
            SET ngayketthuc = km.ngayketthuc + interval '23 hours 59 minutes 59.999 seconds'
            WHERE km.ngayketthuc::time = time '00:00:00'
              AND NOT EXISTS (SELECT 1 FROM khuyenmaisanpham ksp WHERE ksp.makm=km.makm);

            CREATE OR REPLACE VIEW sanphamkhuyenmai_hientai AS
            WITH muc_giam AS (
                SELECT DISTINCT ON (ksp.masku)
                    ksp.masku, ksp.makm, ksp.phantramgiam, km.ngaybatdau, km.ngayketthuc
                FROM khuyenmaisanpham ksp
                JOIN khuyenmai km ON km.makm=ksp.makm
                WHERE km.trangthai=TRUE
                  AND (CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Ho_Chi_Minh')
                      BETWEEN km.ngaybatdau AND km.ngayketthuc
                ORDER BY ksp.masku, ksp.phantramgiam DESC, ksp.makm
            )
            SELECT mg.makm AS makhuyenmai,
                   sp.masp AS masanpham,
                   sp.masku,
                   sp.tensanpham,
                   sp.mausac,
                   sp.kichco,
                   sp.dongia AS giagoc,
                   mg.phantramgiam,
                   ROUND(sp.dongia * (100-mg.phantramgiam)/100, 0) AS giakhuyenmai,
                   mg.ngaybatdau,
                   mg.ngayketthuc,
                   sp.hinhanh
            FROM muc_giam mg JOIN sanpham sp ON sp.masku=mg.masku
            WHERE sp.trangthai=TRUE;

            GRANT SELECT ON sanphamkhuyenmai_hientai TO anon, authenticated;
            """;
        await promotionProducts.ExecuteNonQueryAsync();
    }
}
