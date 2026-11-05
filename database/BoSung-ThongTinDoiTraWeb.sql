-- Chỉ bổ sung thông tin trên phiếu đổi/trả; không đổi tồn kho, tiền hoặc tạo bảng.
BEGIN;
ALTER TABLE public.phieudoitra
    ADD COLUMN IF NOT EXISTS tennguoinhandoi varchar(150),
    ADD COLUMN IF NOT EXISTS sodienthoaidoi varchar(20),
    ADD COLUMN IF NOT EXISTS diachidoihang varchar(300),
    ADD COLUMN IF NOT EXISTS chutaikhoanhoantien varchar(150),
    ADD COLUMN IF NOT EXISTS sotaikhoanhoantien varchar(34),
    ADD COLUMN IF NOT EXISTS tennganhanghoantien varchar(100),
    ADD COLUMN IF NOT EXISTS ngayhoantien timestamp without time zone;
COMMIT;
