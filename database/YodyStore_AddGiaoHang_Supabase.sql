-- Chạy trong Supabase SQL Editor nếu muốn thêm cột trước khi khởi động API mới.
ALTER TABLE public.hoadon
    ADD COLUMN IF NOT EXISTS tennguoinhan varchar(150) NULL;

ALTER TABLE public.hoadon
    ADD COLUMN IF NOT EXISTS nguondon varchar(20) NOT NULL DEFAULT 'POS';

ALTER TABLE public.hoadon
    ADD COLUMN IF NOT EXISTS ghichugiaohang varchar(500) NULL;

CREATE INDEX IF NOT EXISTS ix_hoadon_giaohang
    ON public.hoadon (loaihoadon, trangthaigiaohang, ngaylap DESC);

COMMENT ON COLUMN public.hoadon.tennguoinhan IS 'Tên người nhận hàng tại địa chỉ giao';
COMMENT ON COLUMN public.hoadon.nguondon IS 'Nguồn tạo đơn: POS hoặc WEB';
COMMENT ON COLUMN public.hoadon.ghichugiaohang IS 'Ghi chú giao hàng để shipper đọc trước khi giao';
