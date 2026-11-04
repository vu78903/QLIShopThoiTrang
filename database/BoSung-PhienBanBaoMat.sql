-- Ban de review: chi ap dung sau khi team thong nhat hai cot phienbanbaomat.
-- Khong tao bang moi. Khong doi thong tin dang nhap, quyen hoac du lieu ho so.
BEGIN;
SET LOCAL lock_timeout = '5s';

ALTER TABLE public.nhanvien ADD COLUMN IF NOT EXISTS phienbanbaomat uuid;
ALTER TABLE public.phanquyen ADD COLUMN IF NOT EXISTS phienbanbaomat uuid;

CREATE OR REPLACE FUNCTION public.capnhatphienbanbaomatnhanvien()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        NEW.phienbanbaomat := gen_random_uuid();
    ELSIF OLD.phienbanbaomat IS NULL OR
          ROW(NEW.manv,NEW.tendangnhap,NEW.matkhau,NEW.maquyen,NEW.trangthai)
          IS DISTINCT FROM
          ROW(OLD.manv,OLD.tendangnhap,OLD.matkhau,OLD.maquyen,OLD.trangthai) THEN
        NEW.phienbanbaomat := gen_random_uuid();
    ELSE
        NEW.phienbanbaomat := OLD.phienbanbaomat;
    END IF;
    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION public.capnhatphienbanbaomatphanquyen()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        NEW.phienbanbaomat := gen_random_uuid();
    ELSIF OLD.phienbanbaomat IS NULL OR
          ROW(NEW.maquyen,NEW.trangthai) IS DISTINCT FROM ROW(OLD.maquyen,OLD.trangthai) THEN
        NEW.phienbanbaomat := gen_random_uuid();
    ELSE
        NEW.phienbanbaomat := OLD.phienbanbaomat;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_phienbanbaomat_nhanvien ON public.nhanvien;
CREATE TRIGGER trg_phienbanbaomat_nhanvien
BEFORE INSERT OR UPDATE ON public.nhanvien
FOR EACH ROW EXECUTE FUNCTION public.capnhatphienbanbaomatnhanvien();

DROP TRIGGER IF EXISTS trg_phienbanbaomat_phanquyen ON public.phanquyen;
CREATE TRIGGER trg_phienbanbaomat_phanquyen
BEFORE INSERT OR UPDATE ON public.phanquyen
FOR EACH ROW EXECUTE FUNCTION public.capnhatphienbanbaomatphanquyen();

UPDATE public.nhanvien SET phienbanbaomat=gen_random_uuid() WHERE phienbanbaomat IS NULL;
UPDATE public.phanquyen SET phienbanbaomat=gen_random_uuid() WHERE phienbanbaomat IS NULL;
ALTER TABLE public.nhanvien ALTER COLUMN phienbanbaomat SET NOT NULL;
ALTER TABLE public.phanquyen ALTER COLUMN phienbanbaomat SET NOT NULL;

COMMIT;
