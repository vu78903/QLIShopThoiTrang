-- TASK 2 - REVIEW FIRST, EXECUTE ONLY ON A DISPOSABLE DEV RESTORE.
-- Original two procedures from DEV export (2026-10-15). Same transaction / stock logic.
-- Only database guard, error message, and DDL wrapper adapted for FashionStoreDB_Dev.
-- Do NOT use this script on FashionStoreDB (production) or a real shared database.
USE FashionStoreDB_Dev;
GO
SET XACT_ABORT ON;
IF DB_NAME() <> N'FashionStoreDB_Dev'
    THROW 55100, N'Wrong database. Procedure DDL was NOT executed.', 1;
IF OBJECT_ID(N'dbo.HoaDon',N'U') IS NULL OR OBJECT_ID(N'dbo.BienTheSanPham',N'U') IS NULL
   OR OBJECT_ID(N'dbo.LichSuTonKho',N'U') IS NULL
    THROW 55101, N'Unexpected DEV schema. Procedure DDL was NOT executed.', 1;
BEGIN TRY
    BEGIN TRANSACTION;

    EXEC sys.sp_executesql N'CREATE OR ALTER PROCEDURE dbo.usp_XacNhanDonOnline
    @SoHD VARCHAR(30),
    @MaNV VARCHAR(20) = NULL,
    @KetQua NVARCHAR(30) = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF DB_NAME() <> N''FashionStoreDB_Dev''
        THROW 54002, N''Procedure requires FashionStoreDB_Dev.'', 1;
    IF @SoHD IS NULL OR LTRIM(RTRIM(@SoHD)) = ''''
        THROW 54003, N''SoHD must be provided.'', 1;

    DECLARE @Loai VARCHAR(10), @TrangThai VARCHAR(20),
            @Giao VARCHAR(20), @ThanhToan VARCHAR(20), @SoDong INT;
    DECLARE @BienDong TABLE (
        MaSKU VARCHAR(40) NOT NULL PRIMARY KEY,
        SoLuongThayDoi INT NOT NULL,
        TonSau INT NOT NULL
    );

    BEGIN TRY
        BEGIN TRANSACTION;
        -- Serialize operations on the SAME invoice across concurrent Web/WinForms calls.
        SELECT @Loai=LoaiHoaDon, @TrangThai=TrangThaiHoaDon,
               @Giao=TrangThaiGiaoHang, @ThanhToan=TrangThaiThanhToan
        FROM dbo.HoaDon WITH (UPDLOCK, HOLDLOCK)
        WHERE SoHD=@SoHD;

        IF @Loai IS NULL
            THROW 54004, N''Invoice does not exist.'', 1;
        IF @Loai <> ''ONLINE''
            THROW 54005, N''Only ONLINE invoices are supported.'', 1;
        IF @MaNV IS NOT NULL AND NOT EXISTS
            (SELECT 1 FROM dbo.NhanVien WHERE MaNV=@MaNV AND TrangThai=1)
            THROW 54006, N''Employee does not exist or is disabled.'', 1;
        SELECT @SoDong=COUNT(*) FROM dbo.ChiTietHoaDon WHERE SoHD=@SoHD;
        IF @SoDong=0
            THROW 54007, N''Invoice has no items.'', 1;

        -- Never silently accept a half-written or inconsistent ledger.
        IF @TrangThai IN (''XAC_NHAN'',''DANG_XU_LY'',''HOAN_TAT'')
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.ChiTietHoaDon AS ct
                       WHERE ct.SoHD=@SoHD AND NOT EXISTS (
                           SELECT 1 FROM dbo.LichSuTonKho AS ls
                           WHERE ls.Nguon=''BAN'' AND ls.MaChungTu=@SoHD
                             AND ls.MaSKU=ct.MaSKU AND ls.SoLuongThayDoi=-ct.SoLuong))
               OR EXISTS (SELECT 1 FROM dbo.LichSuTonKho AS ls
                       WHERE ls.Nguon=''BAN'' AND ls.MaChungTu=@SoHD
                         AND NOT EXISTS (SELECT 1 FROM dbo.ChiTietHoaDon AS ct
                           WHERE ct.SoHD=@SoHD AND ct.MaSKU=ls.MaSKU))
               OR EXISTS (SELECT 1 FROM dbo.LichSuTonKho
                       WHERE MaChungTu=@SoHD AND Nguon=''HUY_DON'')
                THROW 54008, N''Existing order stock ledger is inconsistent; do not deduct again.'', 1;
            SET @KetQua=N''DA_XAC_NHAN'';
            COMMIT TRANSACTION;
            RETURN;
        END;

        IF @TrangThai <> ''MOI''
            THROW 54009, N''Only a new (MOI) invoice can be confirmed.'', 1;
        IF @Giao NOT IN (''CHUA_GIAO'',''CHO_XAC_NHAN'')
            THROW 54010, N''Invoice has already advanced to shipping.'', 1;
        IF @ThanhToan NOT IN (''CHUA_THANH_TOAN'',''DA_THANH_TOAN'')
            THROW 54011, N''Payment state does not allow confirmation.'', 1;
        IF EXISTS (SELECT 1 FROM dbo.LichSuTonKho
                   WHERE MaChungTu=@SoHD AND Nguon IN (''BAN'',''HUY_DON''))
            THROW 54012, N''Stock ledger already exists for this new invoice.'', 1;

        -- Atomic conditional stock update; ROWCOUNT verifies ALL SKUs succeeded.
        -- UPDLOCK & UPDATE locks protect against concurrent sales of final units.
        UPDATE bt WITH (UPDLOCK)
           SET SoLuongTonKho=bt.SoLuongTonKho-ct.SoLuong
           OUTPUT inserted.MaSKU,
                  inserted.SoLuongTonKho-deleted.SoLuongTonKho,
                  inserted.SoLuongTonKho
             INTO @BienDong(MaSKU,SoLuongThayDoi,TonSau)
        FROM dbo.BienTheSanPham AS bt
        INNER JOIN dbo.ChiTietHoaDon AS ct ON ct.MaSKU=bt.MaSKU AND ct.SoHD=@SoHD
        INNER JOIN dbo.SanPham AS sp ON sp.MaSP=bt.MaSP
        WHERE bt.TrangThai=1 AND sp.TrangThai=1
          AND bt.SoLuongTonKho>=ct.SoLuong;

        IF (SELECT COUNT(*) FROM @BienDong) <> @SoDong
            THROW 54013, N''Insufficient stock, invalid SKU, or inactive product; no stock deducted.'', 1;

        INSERT dbo.LichSuTonKho
            (MaSKU,MaNV,Nguon,SoLuongThayDoi,TonSau,MaChungTu,GhiChu)
        SELECT MaSKU,@MaNV,''BAN'',SoLuongThayDoi,TonSau,@SoHD,
               N''ONLINE: stock deducted on order confirmation (Option B)''
        FROM @BienDong;

        UPDATE dbo.HoaDon
           SET TrangThaiHoaDon=''XAC_NHAN'', TrangThaiGiaoHang=''DANG_CHUAN_BI''
        WHERE SoHD=@SoHD;

        INSERT dbo.LichSuTrangThaiDonHang
            (SoHD,LoaiTrangThai,TrangThaiCu,TrangThaiMoi,MaNV,GhiChu)
        VALUES (@SoHD,''DON_HANG'',''MOI'',''XAC_NHAN'',@MaNV,N''Confirmed ONLINE order''),
               (@SoHD,''GIAO_HANG'',@Giao,''DANG_CHUAN_BI'',@MaNV,N''Packing preparation'');

        SET @KetQua=N''DA_XAC_NHAN_MOI'';
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;';

    EXEC sys.sp_executesql N'CREATE OR ALTER PROCEDURE dbo.usp_HuyDonOnline
    @SoHD VARCHAR(30),
    @MaNV VARCHAR(20) = NULL,
    @LyDo NVARCHAR(500) = NULL,
    @KetQua NVARCHAR(30) = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF DB_NAME() <> N''FashionStoreDB_Dev''
        THROW 54100, N''Procedure requires FashionStoreDB_Dev.'', 1;
    IF @SoHD IS NULL OR LTRIM(RTRIM(@SoHD)) = ''''
        THROW 54101, N''SoHD must be provided.'', 1;

    DECLARE @Loai VARCHAR(10), @TrangThai VARCHAR(20),
            @Giao VARCHAR(20), @ThanhToan VARCHAR(20), @SoDong INT;
    DECLARE @BienDong TABLE (
        MaSKU VARCHAR(40) NOT NULL PRIMARY KEY,
        SoLuongThayDoi INT NOT NULL,
        TonSau INT NOT NULL
    );
    BEGIN TRY
        BEGIN TRANSACTION;
        SELECT @Loai=LoaiHoaDon, @TrangThai=TrangThaiHoaDon,
               @Giao=TrangThaiGiaoHang, @ThanhToan=TrangThaiThanhToan
        FROM dbo.HoaDon WITH (UPDLOCK, HOLDLOCK)
        WHERE SoHD=@SoHD;
        IF @Loai IS NULL
            THROW 54102, N''Invoice does not exist.'', 1;
        IF @Loai <> ''ONLINE''
            THROW 54103, N''Only ONLINE invoices are supported.'', 1;
        IF @MaNV IS NOT NULL AND NOT EXISTS
            (SELECT 1 FROM dbo.NhanVien WHERE MaNV=@MaNV AND TrangThai=1)
            THROW 54104, N''Employee does not exist or is disabled.'', 1;
        SELECT @SoDong=COUNT(*) FROM dbo.ChiTietHoaDon WHERE SoHD=@SoHD;

        IF @TrangThai=''HUY''
        BEGIN
            -- Do not silently mask previously canceled orders with inconsistent shipping states.
            IF @Giao <> ''HUY''
                THROW 54115, N''Legacy canceled invoice has inconsistent shipping status; review manually.'', 1;
            IF EXISTS (SELECT 1 FROM dbo.LichSuTonKho ls
                       WHERE ls.MaChungTu=@SoHD AND ls.Nguon=''BAN'')
            BEGIN
                IF EXISTS (SELECT 1 FROM dbo.ChiTietHoaDon ct
                    WHERE ct.SoHD=@SoHD AND (
                       NOT EXISTS (SELECT 1 FROM dbo.LichSuTonKho ls
                         WHERE ls.MaChungTu=@SoHD AND ls.MaSKU=ct.MaSKU
                           AND ls.Nguon=''BAN'' AND ls.SoLuongThayDoi=-ct.SoLuong)
                       OR NOT EXISTS (SELECT 1 FROM dbo.LichSuTonKho ls
                         WHERE ls.MaChungTu=@SoHD AND ls.MaSKU=ct.MaSKU
                           AND ls.Nguon=''HUY_DON'' AND ls.SoLuongThayDoi=ct.SoLuong)))
                   OR EXISTS (SELECT 1 FROM dbo.LichSuTonKho ls
                         WHERE ls.MaChungTu=@SoHD AND ls.Nguon IN (''BAN'',''HUY_DON'')
                           AND NOT EXISTS (SELECT 1 FROM dbo.ChiTietHoaDon ct
                            WHERE ct.SoHD=@SoHD AND ct.MaSKU=ls.MaSKU))
                    THROW 54105, N''Canceled invoice has inconsistent stock ledger.'', 1;
            END
            ELSE IF EXISTS (SELECT 1 FROM dbo.LichSuTonKho ls
                            WHERE ls.MaChungTu=@SoHD AND ls.Nguon=''HUY_DON'')
                    THROW 54106, N''Cancellation ledger exists without prior deduction.'', 1;
            SET @KetQua=N''DA_HUY'';
            COMMIT TRANSACTION;
            RETURN;
        END;

        IF @TrangThai NOT IN (''MOI'',''XAC_NHAN'',''DANG_XU_LY'')
            THROW 54107, N''Order cannot be canceled at this status.'', 1;
        IF @Giao NOT IN (''CHUA_GIAO'',''CHO_XAC_NHAN'',''DANG_CHUAN_BI'')
            THROW 54108, N''Shipped/delivered order must follow return workflow.'', 1;
        -- Prevent canceling a prepaid order without financial refund handling.
        IF @ThanhToan NOT IN (''CHUA_THANH_TOAN'',''THAT_BAI'')
            THROW 54109, N''Paid/refunded order needs a separate refund workflow before cancellation.'', 1;

        IF @TrangThai=''MOI''
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.LichSuTonKho
                       WHERE MaChungTu=@SoHD AND Nguon IN (''BAN'',''HUY_DON''))
                THROW 54110, N''New order unexpectedly has stock movement history.'', 1;
        END
        ELSE
        BEGIN
            IF @SoDong=0
                THROW 54111, N''Confirmed invoice has no items.'', 1;
            IF EXISTS (SELECT 1 FROM dbo.ChiTietHoaDon ct
                WHERE ct.SoHD=@SoHD AND NOT EXISTS (
                   SELECT 1 FROM dbo.LichSuTonKho ls
                   WHERE ls.MaChungTu=@SoHD AND ls.MaSKU=ct.MaSKU
                      AND ls.Nguon=''BAN'' AND ls.SoLuongThayDoi=-ct.SoLuong))
               OR EXISTS (SELECT 1 FROM dbo.LichSuTonKho ls
                      WHERE ls.MaChungTu=@SoHD AND ls.Nguon=''BAN''
                        AND NOT EXISTS (SELECT 1 FROM dbo.ChiTietHoaDon ct
                            WHERE ct.SoHD=@SoHD AND ct.MaSKU=ls.MaSKU))
                THROW 54112, N''Confirmed order has no complete BAN ledger.'', 1;
            IF EXISTS (SELECT 1 FROM dbo.LichSuTonKho
                       WHERE MaChungTu=@SoHD AND Nguon=''HUY_DON'')
                THROW 54113, N''Cancellation stock restore already recorded.'', 1;

            -- Overflow-aware restock, all-or-nothing.
            UPDATE bt WITH (UPDLOCK)
               SET SoLuongTonKho=bt.SoLuongTonKho+ct.SoLuong
               OUTPUT inserted.MaSKU,
                      inserted.SoLuongTonKho-deleted.SoLuongTonKho,
                      inserted.SoLuongTonKho
                 INTO @BienDong(MaSKU,SoLuongThayDoi,TonSau)
            FROM dbo.BienTheSanPham AS bt
            JOIN dbo.ChiTietHoaDon AS ct ON ct.MaSKU=bt.MaSKU AND ct.SoHD=@SoHD
            WHERE CAST(bt.SoLuongTonKho AS BIGINT)+ct.SoLuong<=2147483647;
            IF (SELECT COUNT(*) FROM @BienDong) <> @SoDong
                THROW 54114, N''Stock restoration failed (SKU missing or overflow).'', 1;

            INSERT dbo.LichSuTonKho
                (MaSKU,MaNV,Nguon,SoLuongThayDoi,TonSau,MaChungTu,GhiChu)
            SELECT MaSKU,@MaNV,''HUY_DON'',SoLuongThayDoi,TonSau,@SoHD,
                   N''ONLINE: restore stock once on cancellation before shipment''
            FROM @BienDong;
        END;

        UPDATE dbo.HoaDon
           SET TrangThaiHoaDon=''HUY'', TrangThaiGiaoHang=''HUY''
        WHERE SoHD=@SoHD;
        INSERT dbo.LichSuTrangThaiDonHang
            (SoHD,LoaiTrangThai,TrangThaiCu,TrangThaiMoi,MaNV,GhiChu)
        VALUES (@SoHD,''DON_HANG'',@TrangThai,''HUY'',@MaNV,
                LEFT(COALESCE(@LyDo,N''Customer or staff canceled before shipment''),500)),
               (@SoHD,''GIAO_HANG'',@Giao,''HUY'',@MaNV,
                LEFT(COALESCE(@LyDo,N''Shipping canceled before shipment''),500));

        SET @KetQua=CASE WHEN @TrangThai=''MOI'' THEN N''DA_HUY_CHUA_TRU_KHO''
                         ELSE N''DA_HUY_DA_HOAN_KHO'' END;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;';


    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
SELECT name,modify_date FROM sys.procedures WHERE name IN ('usp_XacNhanDonOnline','usp_HuyDonOnline');
GO
