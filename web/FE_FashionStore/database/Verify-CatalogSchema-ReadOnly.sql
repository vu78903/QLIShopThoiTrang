/* Validate the 24 columns used by the Product API on the restored DEV copy.
   Read-only for permanent objects. Does not run migration/install/seed.
   PASS here is only the catalog mapping contract, not a full database audit. */
SET NOCOUNT ON;
IF DB_NAME() <> N'FashionStoreDB_Dev'
    THROW 51000, N'Select the restored FashionStoreDB_Dev copy.', 1;
IF ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'VIEW DEFINITION'),0) <> 1
    THROW 51001, N'VIEW DEFINITION required for the metadata check.', 1;

DECLARE @Expected TABLE (
    TableName sysname, ColumnName sysname, TypeName sysname,
    MaxLength smallint NULL, [Precision] tinyint NULL, Scale tinyint NULL, IsNullable bit
);
INSERT @Expected VALUES
('SanPham','MaSP','varchar',20,NULL,NULL,0),
('SanPham','MaLoai','varchar',20,NULL,NULL,0),
('SanPham','TenSanPham','nvarchar',400,NULL,NULL,0),
('SanPham','MoTa','nvarchar',-1,NULL,NULL,1),
('SanPham','DonGiaNiemYet','decimal',NULL,18,2,0),
('SanPham','DoiTuong','varchar',10,NULL,NULL,1),
('SanPham','TrangThai','bit',NULL,NULL,NULL,0),
('SanPham','NgayTao','datetime2',NULL,NULL,0,0),
('LoaiSanPham','MaLoai','varchar',20,NULL,NULL,0),
('LoaiSanPham','TenLoai','nvarchar',200,NULL,NULL,0),
('LoaiSanPham','TrangThai','bit',NULL,NULL,NULL,0),
('BienTheSanPham','MaSKU','varchar',40,NULL,NULL,0),
('BienTheSanPham','MaSP','varchar',20,NULL,NULL,0),
('BienTheSanPham','KichCo','nvarchar',60,NULL,NULL,0),
('BienTheSanPham','MauSac','nvarchar',120,NULL,NULL,0),
('BienTheSanPham','DonGia','decimal',NULL,18,2,0),
('BienTheSanPham','SoLuongTonKho','int',NULL,NULL,NULL,0),
('BienTheSanPham','TrangThai','bit',NULL,NULL,NULL,0),
('HinhAnhSanPham','MaHinh','bigint',NULL,NULL,NULL,0),
('HinhAnhSanPham','MaSP','varchar',20,NULL,NULL,0),
('HinhAnhSanPham','MaSKU','varchar',40,NULL,NULL,1),
('HinhAnhSanPham','DuongDan','nvarchar',1000,NULL,NULL,0),
('HinhAnhSanPham','AnhChinh','bit',NULL,NULL,NULL,0),
('HinhAnhSanPham','ThuTu','int',NULL,NULL,NULL,0);

DECLARE @Problems TABLE (TableName sysname, ColumnName sysname, Problem nvarchar(200));
INSERT @Problems
SELECT e.TableName,e.ColumnName,N'Missing column or incompatible type/length/precision/scale/nullability'
FROM @Expected e
LEFT JOIN sys.schemas s ON s.name=N'dbo'
LEFT JOIN sys.tables t ON t.schema_id=s.schema_id AND t.name=e.TableName
LEFT JOIN sys.columns c ON c.object_id=t.object_id AND c.name=e.ColumnName
LEFT JOIN sys.types ty ON ty.user_type_id=c.user_type_id
WHERE c.column_id IS NULL OR ty.name<>e.TypeName OR c.is_nullable<>e.IsNullable
   OR (e.MaxLength IS NOT NULL AND c.max_length<>e.MaxLength)
   OR (e.[Precision] IS NOT NULL AND c.precision<>e.[Precision])
   OR (e.Scale IS NOT NULL AND c.scale<>e.Scale);

INSERT @Problems
SELECT keys.TableName,keys.ColumnName,N'Expected single-column primary key is missing'
FROM (VALUES ('SanPham','MaSP'),('LoaiSanPham','MaLoai'),
             ('BienTheSanPham','MaSKU'),('HinhAnhSanPham','MaHinh')) keys(TableName,ColumnName)
WHERE NOT EXISTS (
    SELECT 1 FROM sys.key_constraints k
    JOIN sys.tables t ON t.object_id=k.parent_object_id
    JOIN sys.schemas s ON s.schema_id=t.schema_id
    JOIN sys.index_columns ic ON ic.object_id=k.parent_object_id AND ic.index_id=k.unique_index_id
    JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
    WHERE s.name='dbo' AND t.name=keys.TableName AND k.type='PK'
      AND ic.key_ordinal=1 AND c.name=keys.ColumnName
      AND NOT EXISTS (SELECT 1 FROM sys.index_columns extra
          WHERE extra.object_id=ic.object_id AND extra.index_id=ic.index_id AND extra.key_ordinal>1)
);

SELECT DB_NAME() AS DatabaseName,
    (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) AS AllTables,
    (SELECT COUNT(*) FROM sys.views WHERE is_ms_shipped=0) AS AllViews,
    (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_ms_shipped=0) AS AllForeignKeys,
    (SELECT COUNT(*) FROM sys.procedures WHERE is_ms_shipped=0) AS AllProcedures;
SELECT * FROM @Problems ORDER BY TableName,ColumnName;
IF EXISTS (SELECT 1 FROM @Problems)
    THROW 51002,N'Catalog schema does not match the Product API. Stop and review the differences.',1;
SELECT N'PASS: Catalog column and primary-key contract only' AS CatalogSchemaContract;
