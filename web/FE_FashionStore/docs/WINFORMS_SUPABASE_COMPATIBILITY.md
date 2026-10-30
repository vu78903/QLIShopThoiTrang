# Đối chiếu WinForms và website — 25/10/2026

Nguồn: `D:\KhoaLuanCuNhan\UIUX_Skill\Winform_FashionStore.zip`.
Chỉ đọc trực tiếp trong ZIP, không chạy API, initializer hoặc SQL của partner.
Các đường dẫn nguồn bên dưới nằm trong `Winform_FashionStore/QLIShopThoiTrang/`.
Đây là hành vi của bản source đã nhận, chưa chứng minh mọi hành vi đang được triển khai trên database chung.

## Sản phẩm

- `database/YodyStore_Split_MaSP_MaSKU.sql` đổi khóa chính sang `masku`,
  tạo index `masp` và unique index `(masp, lower(trim(mausac)), lower(trim(kichco)))`.
- `api/QLIShopThoiTrang.Api/Data/SqlServerDatabaseInitializer.cs:60` cũng thực hiện
  chuyển khóa khi khởi động. Điều này giải thích vì sao bản export cũ có khóa `masp`
  nhưng API thực tế sau đó có nhiều dòng cùng mã sản phẩm.
- Mapping website theo SKU và gộp theo `masp` phù hợp với migration trong ZIP.
- Không cần chạy lại script chuyển khóa chỉ để sửa hiển thị website.

## Kết nối và khởi động

- `Data/SqlServerConnectionFactory.cs` dùng Npgsql và biến `YODYSTORE_CONNECTION_STRING`,
  dù tên class vẫn có chữ SqlServer.
- Initializer tự đổi tên bảng/cột sang chữ thường, thêm cột giao hàng và sửa khóa sản phẩm.
  Vì vậy việc chạy API của partner có thể thay đổi schema, không phải chỉ đọc.

## Đơn hàng dùng chung

Nguồn: `api/QLIShopThoiTrang.Api/Data/StoreRepository.cs`.

- `CreateInvoiceAsync` (dòng 605): yêu cầu EmployeeId, kiểm tra nhân viên đang hoạt động.
  Chưa có quy tắc chọn nhân viên tự động cho khách đặt hàng trên website.
- Nguồn đơn dùng `nguondon='WEB'` hoặc `'POS'`.
- Loại đơn khi tạo: `COD`, `GIAO_HANG`, `TAI_QUAY`.
  Luồng cập nhật giao hàng còn nhận `WEB_COD`, `WEB_DA_THANH_TOAN`, `WEB_THANH_TOAN`.
- COD bắt đầu `CHUA_THANH_TOAN`; đơn không COD được code này coi là `DA_THANH_TOAN`.
  Website không được áp dụng điều đó cho chuyển khoản chưa được xác nhận.
- Trạng thái giao: `Vừa xác nhận đơn` → `Shipper đã lấy hàng` →
  `Hàng đang được vận chuyển` → `Hàng giao thành công`.
- COD chỉ gọi hoàn tất hóa đơn khi giao thành công; đơn không COD gọi khi tạo hóa đơn.
- `CompleteInvoiceAsync` (dòng 740) giảm cả `soluongtonkho` và `soluong`,
  tăng tổng chi tiêu và điểm khách hàng. Website không được trừ kho lần nữa cho cùng đơn.
- `DeletePendingCodAsync` xóa chi tiết và hóa đơn COD chưa giao thành công.
  Không mặc định dùng thao tác xóa này làm chính sách khách tự hủy đơn trên web.

## Các khác biệt cần xử lý trước khi bật chức năng ghi trên website

- Tài khoản khách hàng và giỏ hàng chưa có trong schema đã nhận.
- `LoginAsync` (dòng 155) so sánh trực tiếp `NhanVien.MatKhau` với đầu vào;
  không tương thích cách xác minh `MatKhauHash` của website hiện tại.
  Không suy ra các mật khẩu đang lưu thực tế chỉ từ đoạn source này.
- API tạo hóa đơn nhận giá/tổng tiền từ DTO: website cần tự tính bằng dữ liệu database,
  không tin các giá trị do trình duyệt gửi lên.
- Luồng kiểm tra tồn rồi cập nhật trong code chưa có khóa dòng rõ ràng;
  cần xử lý giao dịch đồng thời giữa POS/web trước khi bật đặt hàng và hoàn tất đơn.
- Cần xác nhận nhân viên phụ trách đơn web, chính sách xác nhận thanh toán,
  hủy đơn và các trigger/function đang tồn tại trên database thật.

Website tiếp tục chỉ đọc catalog trong chế độ Supabase. Bước kế tiếp là xác minh
danh sách và chi tiết nhiều SKU sau restart; sau đó thiết kế phần tài khoản/giỏ hàng
và quy tắc đơn hàng dựa trên cấu trúc chung này.
