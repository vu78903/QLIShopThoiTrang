-- READ ONLY. Change the @SoHD value to the order ID shown on the website.
USE FashionStoreDB_Dev;
GO
DECLARE @SoHD varchar(30) = 'REPLACE_WITH_ORDER_ID';
SELECT SoHD,LoaiHoaDon,MaKH,TrangThaiHoaDon,TrangThaiGiaoHang,TrangThaiThanhToan,
       PhuongThucThanhToan,TongTienHang,KhachPhaiTra,TienCOD,TenNguoiNhan
FROM dbo.HoaDon WHERE SoHD=@SoHD;
SELECT SoHD,MaSKU,SoLuong,DonGia,ThanhTien,TenSanPhamLucMua
FROM dbo.ChiTietHoaDon WHERE SoHD=@SoHD;
SELECT MaThanhToan,SoHD,SoTien,PhuongThuc,TrangThai,LoaiGiaoDich
FROM dbo.ThanhToan WHERE SoHD=@SoHD;
SELECT SoHD,LoaiTrangThai,TrangThaiCu,TrangThaiMoi,MaNV,ThoiDiem
FROM dbo.LichSuTrangThaiDonHang WHERE SoHD=@SoHD ORDER BY MaLichSu;
SELECT MaSKU,Nguon,SoLuongThayDoi,TonSau,MaChungTu,ThoiDiem
FROM dbo.LichSuTonKho WHERE MaChungTu=@SoHD ORDER BY MaBienDong;
-- On MOI: no BAN/ HUY_DON rows. On XAC_NHAN: one BAN row per order line.
-- On HUY from XAC_NHAN: one BAN and one HUY_DON per line.
