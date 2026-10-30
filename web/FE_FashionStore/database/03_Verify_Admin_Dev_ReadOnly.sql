-- Safe read-only inspection of existing employee accounts and permission codes.
USE [FashionStoreDB_Dev];
GO
SELECT MaQuyen, TenQuyen, TrangThai FROM dbo.PhanQuyen ORDER BY MaQuyen;
SELECT MaNV, HoTen, MaQuyen, TrangThai,
 CASE WHEN MatKhauHash IS NULL THEN 0 ELSE 1 END AS DaCoMatKhau
FROM dbo.NhanVien ORDER BY MaNV;
