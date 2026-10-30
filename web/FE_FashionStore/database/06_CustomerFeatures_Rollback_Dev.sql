-- Safe rollback: refuses to discard any user data. Back up and archive first if nonempty.
IF DB_NAME()<>N'FashionStoreDB_Dev' THROW 55300,N'Open FashionStoreDB_Dev first.',1;
SET XACT_ABORT ON;
BEGIN TRAN;
IF OBJECT_ID(N'dbo.WebWishlist',N'U') IS NOT NULL
BEGIN
 IF EXISTS(SELECT 1 FROM dbo.WebWishlist WITH(TABLOCKX,HOLDLOCK)) THROW 55301,N'Wishlist contains data; rollback refused.',1;
 DROP TABLE dbo.WebWishlist;
END;
IF OBJECT_ID(N'dbo.WebAddress',N'U') IS NOT NULL
BEGIN
 IF EXISTS(SELECT 1 FROM dbo.WebAddress WITH(TABLOCKX,HOLDLOCK)) THROW 55301,N'Address contains data; rollback refused.',1;
 DROP TABLE dbo.WebAddress;
END;
IF OBJECT_ID(N'dbo.WebPasswordReset',N'U') IS NOT NULL
BEGIN
 IF EXISTS(SELECT 1 FROM dbo.WebPasswordReset WITH(TABLOCKX,HOLDLOCK)) THROW 55301,N'Reset contains data; rollback refused.',1;
 DROP TABLE dbo.WebPasswordReset;
END;
COMMIT;
