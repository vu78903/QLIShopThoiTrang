# Task 2 — Checkout, Online Order & Admin Orders (DEV pilot)

## Phạm vi đã viết

- Customer: `/Checkout`, `/Orders`, `/Orders/Details/{id}` (đăng nhập riêng của khách hàng).
- API Customer: `POST /api/orders`, `GET /api/orders`, `GET /api/orders/{id}`.
- Admin: `/Admin/Orders`, `/Admin/Order/{id}`; nút xác nhận/hủy đơn.
- API Admin: `GET /api/admin/orders`, `GET /api/admin/orders/{id}`, `POST .../{id}/confirm`, `POST .../{id}/cancel`.
- Các bảng có sẵn: `HoaDon`, `ChiTietHoaDon`, `ThanhToan`, `LichSuTrangThaiDonHang`, `GioHang`, `ChiTietGioHang`.
- Giữ Task 1: `/Admin/Login`, `/Admin/Index`, Account, Cart, Catalog. KHÔNG thay mật khẩu hay ghi đè DB.

## Chuẩn bị

1. Backup `FashionStoreDB_Dev` trước. Tuyệt đối **không chạy** trên `FashionStoreDB` production.
2. Dùng SSMS kiểm tra đang kết nối SQL Server `LAPTOP-JUQP99HG`, database `FashionStoreDB_Dev`.
3. Mở và **review** `database/04_OrderProcedures_Dev_ReviewFirst.sql` trước khi chạy. Script này copy chính xác hai procedure gốc từ export schema, chỉ điều chỉnh guard từ `FashionStoreDB` thành `FashionStoreDB_Dev` và dùng `CREATE OR ALTER`. Có guard bảo vệ trước khi thực thi DDL trong một transaction.
4. Script không đụng đến các procedure đổi trả. Nếu bạn đã chỉnh hai procedure này riêng sau snapshot 15/10, **không chạy chồng**; so sánh lại trước.
5. Mở `FashionStore.sln` trong Visual Studio 2022 và chạy **Restore NuGet Packages**. Giữ `.NET Framework 4.8` MVC + `.NET 8` API. Build Solution; nếu thấy lỗi `roslyn/csc.exe` thì xem `docs/FIX_ROSLYN_CSC.md`.
6. Chạy `FashionStore.Backend` ở `http://127.0.0.1:5000` (Swagger `/swagger`, DB `/health/ready`); chạy `FashionStore.Web` trong IIS Express. Mặc định site `http://localhost:58472` (port có thể khác nếu bạn đổi cấu hình).

## Demo từng bước

1. Mở tài khoản Customer có sẵn (không sử dụng user `admin`). Vào Sản phẩm → chọn SKU size/màu → thêm vào Cart.
2. Trong Cart, kiểm tra giá, số lượng, khả năng đặt hàng; nhấn **TIẾN HÀNH THANH TOÁN**.
3. Điền người nhận, SĐT, địa chỉ; chọn **COD** hoặc **CHUYEN_KHOAN** (chỉ đánh dấu đang chờ nhân viên kiểm tra, không có gateway). Nhấn **ĐẶT HÀNG** một lần.
4. Ghi lại mã `ON...` từ trang chi tiết. Trong DB đơn phải là `MOI`, tồn kho chưa trừ, trạng thái thanh toán `CHUA_THANH_TOAN`, có bản ghi `ThanhToan` ở `CHO_XU_LY`.
5. Đăng nhập `admin` tại `/Admin/Login`, mở `/Admin/Orders`, xem mã đơn rồi nhấn **Xác nhận đơn**. Database phải có `XAC_NHAN`, xuất đúng số lượng trong `LichSuTonKho` với `Nguon='BAN'`.
6. Với đơn chưa giao/ chưa thanh toán, nhấn **Hủy đơn** (hoàn tồn kho đúng một lần); kiểm tra `Nguon='HUY_DON'` và trạng thái `HUY`.
7. Đăng nhập lại Customer → `/Orders` → lịch sử và trạng thái vừa đổi. Chạy `database/05_Verify_Order_Dev_ReadOnly.sql`, thay `@SoHD` trước.

## Phạm vi và giới hạn cần nêu khi báo cáo

- **Đang ở bản DEV pilot, chưa chạy kiểm thử tích hợp SQL trên máy người dùng.** Chưa thể cam kết build/runtime PASS nếu chưa chạy trong Visual Studio + SQL Server.
- **Chỉ hoạt động với `FashionStoreDB_Dev`.** `OrderService` sẽ từ chối DB khác nhằm bảo vệ DB thật trong quá trình làm khóa luận. Khi tích hợp với môi trường thật phải có quy trình triển khai/test riêng.
- Chưa có chuyển trạng thái `DANG_GIAO` / `HOAN_TAT`, xác minh chuyển khoản thật, đối soát COD, phí ship, khuyến mãi/điểm, đổi trả, gửi email, hay cổng thanh toán.
- Không cho phép Customer tự đổi trạng thái đơn. Admin chỉ xác nhận/hủy theo rule của stored procedure gốc.
- `MOI` không giữ hàng và không trừ tồn; nếu hai khách đặt quá lượng kho cùng lúc, người xác nhận sau có thể nhận 409 và cần xử lý lại. Đây là nghiệp vụ tồn kho Option B đã chốt, không được tự thay đổi thành trừ ngay khi đặt.
- POST checkout có `RequestId` gắn với form; gửi lại form cùng mã sẽ trả đơn đã tạo thay vì tạo đơn trùng. Gửi form khác là đặt đơn mới.
- `ThanhToan` được tạo trạng thái `CHO_XU_LY`, không đánh dấu tự động là đã nhận tiền. Nếu Admin cố hủy đơn đã thanh toán, proc sẽ chặn cho tới khi có luồng hoàn tiền riêng.
- Biên dịch/test toàn solution có thể cần Windows + Visual Studio 2022; môi trường tạo artifact không có .NET SDK/IIS Express/SQL Server.

## Kiểm tra bảo mật thủ công

- Guest truy cập `/Checkout`, `/Orders` phải chuyển đến login Customer.
- Customer token không thể gọi `/api/admin/orders` (401/403).
- Guest hoặc admin token không thể gọi `/api/orders` Customer (401/403).
- Khách A gọi `/api/orders/{id}` của khách B phải 404.
- F5 sau POST (redirect) không tạo đơn thứ hai; đồng thời kiểm tra retries cùng `RequestId`.
- Đổi `quantity`/`unitPrice` trong request ở trình duyệt không tác động đến giá và tồn thực tế; server chỉ lấy từ SQL.
- Xác nhận/hủy cùng đơn 2 lần không được trừ kho hoặc hoàn kho lần thứ hai.
- Backend không chạy hoặc mất kết nối không được hiển thị giả một đơn hoàn tất.

## File quan trọng

- `FashionStore.Backend/Services/OrderService.cs`
- `FashionStore.Backend/Controllers/OrdersController.cs`
- `FashionStore.Backend/Controllers/AdminOrdersController.cs`
- `FashionStore.Web/Controllers/CheckoutController.cs`
- `FashionStore.Web/Controllers/OrdersController.cs`
- `FashionStore.Web/Controllers/AdminController.cs`
- `database/04_OrderProcedures_Dev_ReviewFirst.sql`
- `database/05_Verify_Order_Dev_ReadOnly.sql`
