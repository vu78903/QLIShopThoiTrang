-- DEVELOPMENT ONLY. Review existing role/person records before executing.
-- No password stored here; run tools/SetAdminPassword after approval.
USE [FashionStoreDB_Dev];
GO
IF DB_NAME() <> N'FashionStoreDB_Dev'
    THROW 51001, N'Refusing to seed outside FashionStoreDB_Dev', 1;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS (SELECT 1 FROM dbo.PhanQuyen WHERE MaQuyen='ADMIN')
BEGIN
    INSERT INTO dbo.PhanQuyen(MaQuyen,TenQuyen,MoTa,TrangThai)
    VALUES ('ADMIN',N'Quản trị website',N'Quản trị website FashionStore (role demo DEV)',1);
END
IF NOT EXISTS (SELECT 1 FROM dbo.NhanVien WHERE MaNV='WEBADMIN001')
BEGIN
    INSERT INTO dbo.NhanVien(MaNV,MaQuyen,HoTen,ChucVu,MatKhauHash,TrangThai)
    VALUES ('WEBADMIN001','ADMIN',N'Quản trị viên web DEMO',N'Quản trị website',NULL,1);
END
COMMIT TRANSACTION;
SELECT MaQuyen,TenQuyen,TrangThai FROM dbo.PhanQuyen WHERE MaQuyen='ADMIN';
SELECT MaNV,HoTen,MaQuyen,TrangThai,CASE WHEN MatKhauHash IS NULL THEN 0 ELSE 1 END AS DaCoMatKhau
FROM dbo.NhanVien WHERE MaNV='WEBADMIN001';
