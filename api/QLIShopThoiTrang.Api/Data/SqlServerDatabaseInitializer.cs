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
    }
}
