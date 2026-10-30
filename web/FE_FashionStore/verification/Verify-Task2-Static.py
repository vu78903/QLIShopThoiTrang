"""Offline checks for Task 2. NOT a substitute for Visual Studio + SQL Server tests."""
import json, pathlib, xml.etree.ElementTree as ET, re
r=pathlib.Path(__file__).resolve().parents[1]
root=ET.parse(r/'FashionStore.Web/FashionStore.Web.csproj').getroot()
items={x.attrib.get('Include') for x in root.iter() if x.tag.split('}')[-1] in ('Compile','Content')}
required={
 'Compile': ['Controllers/CheckoutController.cs','Controllers/OrdersController.cs','Models/OrderModels.cs'],
 'Content': ['Views/Checkout/Index.cshtml','Views/Orders/Index.cshtml','Views/Orders/Details.cshtml','Views/Admin/Orders.cshtml','Views/Admin/Order.cshtml']
}
results=[]
for k,v in required.items():
 for filename in v:
  path=filename.replace('/','\\')
  good=path in items and (r/'FashionStore.Web'/filename).exists()
  results.append((k+' '+filename,good))
expected={'HoaDon':{'SoHD','MaKH','LoaiHoaDon','PhuongThucThanhToan','TrangThaiHoaDon','TrangThaiGiaoHang','TrangThaiThanhToan','TongTienHang','TongCong','KhachPhaiTra','TienCOD','TenNguoiNhan','SoDienThoaiNhan','DiaChiGiao','GhiChu'},'ChiTietHoaDon':{'SoHD','MaSKU','SoLuong','DonGia','TenSanPhamLucMua','KichCoLucMua','MauSacLucMua'},'ThanhToan':{'SoHD','SoTien','PhuongThuc','TrangThai','LoaiGiaoDich','GhiChu'},'LichSuTrangThaiDonHang':{'SoHD','LoaiTrangThai','TrangThaiMoi','GhiChu'},'LichSuTonKho':{'Nguon','MaChungTu','MaSKU','SoLuongThayDoi','TonSau'}}
schema=json.loads((r/'database/schema-reference/FashionStoreDB_Dev_schema_20261015_170000_433_bf029ba2.json').read_text())
for t, names in expected.items():
 dbnames={c['name'] for c in schema['columns'] if c['object']==t}
 results.append(('SQL columns '+t, names <= dbnames))
sql=(r/'database/04_OrderProcedures_Dev_ReviewFirst.sql').read_text()
results.append(('SQL guard and transaction', "IF DB_NAME() <> N'FashionStoreDB_Dev'" in sql and 'BEGIN TRANSACTION;' in sql and sql.count('CREATE OR ALTER PROCEDURE')==2))
results.append(('Only two SQL procedures patched', all('dbo.'+s in sql for s in ['usp_XacNhanDonOnline','usp_HuyDonOnline'])))
service=(r/'FashionStore.Backend/Services/OrderService.cs').read_text()
results.append(('DEV database guard', 'FashionStoreDB_Dev' in service and 'builder.InitialCatalog' in service))
results.append(('Procedure confirmation/cancellation', 'dbo.usp_XacNhanDonOnline' in service and 'dbo.usp_HuyDonOnline' in service))
results.append(('Idempotency hash', 'SHA256.HashData' in service and 'RequestId' in service and 'HOLDLOCK' in service))
results.append(('No checkout stock decrement', not re.search(r'SoLuongTonKho\s*=\s*.*-',service)))
results.append(('Payment not marked succeeded', "'CHO_XU_LY'" in service and 'THANH_CONG' not in service))
results.append(('Both roles separate endpoints', '[Authorize' not in service and 'AdminAuthentication.SchemeName' in (r/'FashionStore.Backend/Controllers/AdminOrdersController.cs').read_text() and 'Roles = "Customer"' in (r/'FashionStore.Backend/Controllers/OrdersController.cs').read_text()))
missing=[s for s,good in results if not good]
for s,good in results:print(('PASS' if good else 'FAIL')+' '+s)
print(f'{len(results)-len(missing)}/{len(results)} checks passed')
if missing:raise SystemExit(1)
