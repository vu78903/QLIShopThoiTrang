-- PostgreSQL lưu tên cột không đặt dấu ngoặc kép thành chữ thường.
ALTER TABLE public.khachhang ADD COLUMN IF NOT EXISTS tendangnhap varchar(50) NULL;
ALTER TABLE public.khachhang ADD COLUMN IF NOT EXISTS matkhau varchar(200) NULL;
ALTER TABLE public.khachhang ADD COLUMN IF NOT EXISTS gioitinh varchar(20) NULL;
ALTER TABLE public.khachhang ADD COLUMN IF NOT EXISTS ngaysinh date NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_khachhang_tendangnhap
    ON public.khachhang(tendangnhap) WHERE tendangnhap IS NOT NULL AND tendangnhap<>'';
-- Website trên máy khác cần ghi hai cột này khi đăng ký, đổi tài khoản hoặc đổi mật khẩu.
-- Mật khẩu đã băm ở bảng tài khoản web cũ không thể chuyển ngược thành mật khẩu gốc.
