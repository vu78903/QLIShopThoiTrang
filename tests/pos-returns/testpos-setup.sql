INSERT INTO nhanvien(manv,tendangnhap,hoten,chucvu,sodienthoai,email,diachi,maquyen,matkhau,trangthai) VALUES
('TESTFIX-MAN','testfixman','TESTPOS quản lý','Quản lý','0998301011','testfixman@example.com','TEST','CHT','TestOnly3010!',TRUE),
('TESTFIX-CASH','testfixcash','TESTPOS thu ngân','Thu ngân','0998301012','testfixcash@example.com','TEST','TN','TestOnly3010!',TRUE);
INSERT INTO khachhang(makh,hoten,sodienthoai,email,diachi,hangthe,tongchitieu,diemtichluy,trangthai)
VALUES('TESTPOS-KH','TESTPOS khách hàng','0998301099','testpos@example.com','TEST','Vàng',6000000,600,TRUE);
INSERT INTO sanpham(masp,masku,maloai,tensanpham,danhmuc,mansx,soluong,kichco,mausac,trangthai,dongia,soluongtonkho,hinhanh,danhsachhinhanh)
SELECT 'SP-999991','TESTPOS-SALE',maloai,'TESTPOS áo thun sale',danhmuc,mansx,100,'M','Đen',TRUE,100000,100,NULL,'[]'::jsonb FROM sanpham WHERE trangthai=TRUE AND danhMuc='NAM' AND mansx='NCC-001' ORDER BY masku LIMIT 1;
INSERT INTO sanpham(masp,masku,maloai,tensanpham,danhmuc,mansx,soluong,kichco,mausac,trangthai,dongia,soluongtonkho,hinhanh,danhsachhinhanh)
SELECT 'SP-999992','TESTPOS-OTHER',maloai,'TESTPOS áo thun thường',danhmuc,mansx,100,'L','Trắng',TRUE,200000,100,NULL,'[]'::jsonb FROM sanpham WHERE masku='TESTPOS-SALE';
INSERT INTO khuyenmai(makm,tenchuongtrinh,loaikm,giatrigiam,giatridonhangtoithieu,ngaybatdau,ngayketthuc,trangthai) VALUES
('TSTPOS-SALE','TESTPOS sale SKU','PHAN_TRAM',25,0,(CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Ho_Chi_Minh')-interval '1 day',(CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Ho_Chi_Minh')+interval '1 day',TRUE),
('TSTPOS-V10','TESTPOS voucher 10','PHAN_TRAM',10,100000,(CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Ho_Chi_Minh')-interval '1 day',(CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Ho_Chi_Minh')+interval '1 day',TRUE),
('TSTPOS-CASH','TESTPOS voucher tiền','SO_TIEN',10000,0,(CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Ho_Chi_Minh')-interval '1 day',(CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Ho_Chi_Minh')+interval '1 day',TRUE);
INSERT INTO khuyenmaisanpham(makm,masku,phantramgiam) VALUES('TSTPOS-SALE','TESTPOS-SALE',25);
SELECT masku,dongia,soluongtonkho FROM sanpham WHERE masku LIKE 'TESTPOS-%';
