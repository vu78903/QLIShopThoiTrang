INSERT INTO nhanvien(manv,tendangnhap,hoten,chucvu,sodienthoai,email,diachi,maquyen,matkhau,trangthai) VALUES
('TEST69-KHO','test69kho','TEST69 nhân viên kho','Nhân viên kho','0998301013','test69kho@example.com','TEST','KHO','TestOnly3010!',TRUE);
INSERT INTO khachhang(makh,hoten,sodienthoai,email,diachi,hangthe,tongchitieu,diemtichluy,trangthai,tendangnhap,matkhau)
VALUES('TEST69-KH','TEST69 khách hàng','0998301014','test69@example.com','TEST','Bạc',0,0,TRUE,'test69customer','TestOnly3010!');
INSERT INTO sanpham(masp,masku,maloai,tensanpham,danhmuc,mansx,soluong,kichco,mausac,trangthai,dongia,soluongtonkho,danhsachhinhanh)
SELECT 'SP-999993','TEST69-SCARCE',maloai,'TEST69 hàng tồn một cái',danhmuc,mansx,1,'M','Đen',TRUE,20000,1,'[]'::jsonb FROM sanpham WHERE masku='TESTPOS-SALE';
