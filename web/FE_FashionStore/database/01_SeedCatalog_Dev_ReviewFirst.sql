/*
  OPTIONAL storefront demo seed for local DEVELOPMENT only.
  Default @ApplyDemo=0 => no changes. NEVER run on FashionStoreDB production.
  This script is independent of the original db1/02_Seed_Demo.sql (v1).
*/
USE [FashionStoreDB_Dev];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @ApplyDemo BIT = 0;

IF DB_NAME() <> N'FashionStoreDB_Dev'
    THROW 51001, 'This seed may ONLY run on FashionStoreDB_Dev.', 1;
IF OBJECT_ID(N'dbo.SanPham', N'U') IS NULL OR COL_LENGTH(N'dbo.SanPham', N'DoiTuong') IS NULL
    THROW 51002, 'Expected FashionStoreDB schema v2 was not found.', 1;
IF @ApplyDemo = 0
BEGIN
    PRINT N'REVIEW-ONLY: No sample catalog data inserted. Set @ApplyDemo=1 for DEV only.';
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM dbo.LoaiSanPham WHERE MaLoai='WEB-NAM')
        INSERT dbo.LoaiSanPham(MaLoai,TenLoai,MoTa,TrangThai) VALUES ('WEB-NAM', N'Mộc Nhiên Demo - Nam', N'Danh mục mẫu dành cho việc chạy thử Website', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.LoaiSanPham WHERE MaLoai='WEB-NU')
        INSERT dbo.LoaiSanPham(MaLoai,TenLoai,MoTa,TrangThai) VALUES ('WEB-NU', N'Mộc Nhiên Demo - Nữ', N'Danh mục mẫu dành cho việc chạy thử Website', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.LoaiSanPham WHERE MaLoai='WEB-UNI')
        INSERT dbo.LoaiSanPham(MaLoai,TenLoai,MoTa,TrangThai) VALUES ('WEB-UNI', N'Mộc Nhiên Demo - Unisex', N'Danh mục mẫu dành cho việc chạy thử Website', 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.SanPham WHERE MaSP='WEB-DEMO-001')
        INSERT dbo.SanPham(MaSP,MaLoai,TenSanPham,MoTa,DonGiaNiemYet,TrangThai,DoiTuong)
        VALUES ('WEB-DEMO-001','WEB-NAM',N'Áo sơ mi linen dáng suông',N'Áo sơ mi linen tối giản, cảm hứng từ những ngày hè thành thị. Đây là dữ liệu mẫu phục vụ giao diện Website.',690000,1,'NAM');
    IF NOT EXISTS (SELECT 1 FROM dbo.SanPham WHERE MaSP='WEB-DEMO-002')
        INSERT dbo.SanPham(MaSP,MaLoai,TenSanPham,MoTa,DonGiaNiemYet,TrangThai,DoiTuong)
        VALUES ('WEB-DEMO-002','WEB-NU',N'Đầm lụa An Nhiên',N'Đầm dáng thanh lịch, phù hợp dạo phố hoặc đi làm. Đây là dữ liệu mẫu phục vụ giao diện Website.',1190000,1,'NU');
    IF NOT EXISTS (SELECT 1 FROM dbo.SanPham WHERE MaSP='WEB-DEMO-003')
        INSERT dbo.SanPham(MaSP,MaLoai,TenSanPham,MoTa,DonGiaNiemYet,TrangThai,DoiTuong)
        VALUES ('WEB-DEMO-003','WEB-NAM',N'Áo khoác dáng dài',N'Áo khoác tối giản cho phong cách hiện đại. Đây là dữ liệu mẫu phục vụ giao diện Website.',1490000,1,'NAM');
    IF NOT EXISTS (SELECT 1 FROM dbo.SanPham WHERE MaSP='WEB-DEMO-004')
        INSERT dbo.SanPham(MaSP,MaLoai,TenSanPham,MoTa,DonGiaNiemYet,TrangThai,DoiTuong)
        VALUES ('WEB-DEMO-004','WEB-UNI',N'Bộ đồ tối giản City Edit',N'Thiết kế đa dụng, dễ phối trong đời sống hàng ngày. Đây là dữ liệu mẫu phục vụ giao diện Website.',890000,1,'UNISEX');

    IF NOT EXISTS (SELECT 1 FROM dbo.BienTheSanPham WHERE MaSKU='WEB-DEMO-001-M')
        INSERT dbo.BienTheSanPham(MaSKU,MaSP,KichCo,MauSac,DonGia,SoLuongTonKho,TrangThai)
        VALUES('WEB-DEMO-001-M','WEB-DEMO-001',N'M',N'Kem',690000,12,1);
    IF NOT EXISTS (SELECT 1 FROM dbo.BienTheSanPham WHERE MaSKU='WEB-DEMO-001-L')
        INSERT dbo.BienTheSanPham(MaSKU,MaSP,KichCo,MauSac,DonGia,SoLuongTonKho,TrangThai)
        VALUES('WEB-DEMO-001-L','WEB-DEMO-001',N'L',N'Kem',690000,8,1);
    IF NOT EXISTS (SELECT 1 FROM dbo.BienTheSanPham WHERE MaSKU='WEB-DEMO-002-S')
        INSERT dbo.BienTheSanPham(MaSKU,MaSP,KichCo,MauSac,DonGia,SoLuongTonKho,TrangThai)
        VALUES('WEB-DEMO-002-S','WEB-DEMO-002',N'S',N'Xanh lá',1190000,10,1);
    IF NOT EXISTS (SELECT 1 FROM dbo.BienTheSanPham WHERE MaSKU='WEB-DEMO-002-M')
        INSERT dbo.BienTheSanPham(MaSKU,MaSP,KichCo,MauSac,DonGia,SoLuongTonKho,TrangThai)
        VALUES('WEB-DEMO-002-M','WEB-DEMO-002',N'M',N'Xanh lá',1190000,3,1);
    IF NOT EXISTS (SELECT 1 FROM dbo.BienTheSanPham WHERE MaSKU='WEB-DEMO-003-M')
        INSERT dbo.BienTheSanPham(MaSKU,MaSP,KichCo,MauSac,DonGia,SoLuongTonKho,TrangThai)
        VALUES('WEB-DEMO-003-M','WEB-DEMO-003',N'M',N'Nâu',1490000,2,1);
    IF NOT EXISTS (SELECT 1 FROM dbo.BienTheSanPham WHERE MaSKU='WEB-DEMO-003-L')
        INSERT dbo.BienTheSanPham(MaSKU,MaSP,KichCo,MauSac,DonGia,SoLuongTonKho,TrangThai)
        VALUES('WEB-DEMO-003-L','WEB-DEMO-003',N'L',N'Nâu',1490000,0,1);
    IF NOT EXISTS (SELECT 1 FROM dbo.BienTheSanPham WHERE MaSKU='WEB-DEMO-004-M')
        INSERT dbo.BienTheSanPham(MaSKU,MaSP,KichCo,MauSac,DonGia,SoLuongTonKho,TrangThai)
        VALUES('WEB-DEMO-004-M','WEB-DEMO-004',N'M',N'Đen',890000,7,1);
    IF NOT EXISTS (SELECT 1 FROM dbo.BienTheSanPham WHERE MaSKU='WEB-DEMO-004-L')
        INSERT dbo.BienTheSanPham(MaSKU,MaSP,KichCo,MauSac,DonGia,SoLuongTonKho,TrangThai)
        VALUES('WEB-DEMO-004-L','WEB-DEMO-004',N'L',N'Đen',890000,5,1);

    IF NOT EXISTS (SELECT 1 FROM dbo.HinhAnhSanPham WHERE MaSP='WEB-DEMO-001' AND MaSKU IS NULL)
        INSERT dbo.HinhAnhSanPham(MaSP,MaSKU,DuongDan,AnhChinh,ThuTu)
        VALUES('WEB-DEMO-001',NULL,N'https://images.unsplash.com/photo-1526413232644-8a40f03cc03b?auto=format&fit=crop&w=900&q=85',1,0);
    IF NOT EXISTS (SELECT 1 FROM dbo.HinhAnhSanPham WHERE MaSP='WEB-DEMO-002' AND MaSKU IS NULL)
        INSERT dbo.HinhAnhSanPham(MaSP,MaSKU,DuongDan,AnhChinh,ThuTu)
        VALUES('WEB-DEMO-002',NULL,N'https://images.unsplash.com/photo-1772443325539-0572726cb8c6?auto=format&fit=crop&w=900&q=85',1,0);
    IF NOT EXISTS (SELECT 1 FROM dbo.HinhAnhSanPham WHERE MaSP='WEB-DEMO-003' AND MaSKU IS NULL)
        INSERT dbo.HinhAnhSanPham(MaSP,MaSKU,DuongDan,AnhChinh,ThuTu)
        VALUES('WEB-DEMO-003',NULL,N'https://images.unsplash.com/photo-1565101129934-0da84e9fd368?auto=format&fit=crop&w=900&q=85',1,0);
    IF NOT EXISTS (SELECT 1 FROM dbo.HinhAnhSanPham WHERE MaSP='WEB-DEMO-004' AND MaSKU IS NULL)
        INSERT dbo.HinhAnhSanPham(MaSP,MaSKU,DuongDan,AnhChinh,ThuTu)
        VALUES('WEB-DEMO-004',NULL,N'https://images.unsplash.com/photo-1526413232644-8a40f03cc03b?auto=format&fit=crop&w=900&q=85',1,0);

    COMMIT TRANSACTION;
    PRINT N'DEV ONLY: Demo catalog seed applied (idempotent inserts). No orders or customer data created.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
