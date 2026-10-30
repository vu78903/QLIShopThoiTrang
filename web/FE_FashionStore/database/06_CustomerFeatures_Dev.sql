-- Proposed minimal Web-only additions. Review and run manually in the DEV restore.
-- Does not change shared WinForms tables/procedures or seed invented records.
IF DB_NAME()<>N'FashionStoreDB_Dev' THROW 55300,N'Open FashionStoreDB_Dev first.',1;
SET XACT_ABORT ON;
BEGIN TRAN;
IF OBJECT_ID(N'dbo.WebWishlist',N'U') IS NULL
CREATE TABLE dbo.WebWishlist(
 MaKH varchar(20) NOT NULL REFERENCES dbo.KhachHang(MaKH),
 MaSP varchar(20) NOT NULL REFERENCES dbo.SanPham(MaSP),
 CreatedAt datetime2(0) NOT NULL DEFAULT SYSDATETIME(),
 CONSTRAINT PK_WebWishlist PRIMARY KEY(MaKH,MaSP));
IF OBJECT_ID(N'dbo.WebAddress',N'U') IS NULL
CREATE TABLE dbo.WebAddress(
 Id bigint IDENTITY PRIMARY KEY,MaKH varchar(20) NOT NULL REFERENCES dbo.KhachHang(MaKH),
 Recipient nvarchar(150) NOT NULL,Phone varchar(20) NOT NULL,Address nvarchar(300) NOT NULL);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.WebAddress') AND name='IX_WebAddress_Customer')
 CREATE INDEX IX_WebAddress_Customer ON dbo.WebAddress(MaKH,Id);
IF OBJECT_ID(N'dbo.WebPasswordReset',N'U') IS NULL
CREATE TABLE dbo.WebPasswordReset(
 TokenHash char(64) NOT NULL PRIMARY KEY,MaKH varchar(20) NOT NULL REFERENCES dbo.TaiKhoanKhachHang(MaKH),
 PasswordVersion char(64) NOT NULL,ExpiresAtUtc datetime2(0) NOT NULL,UsedAtUtc datetime2(0) NULL);
COMMIT;
