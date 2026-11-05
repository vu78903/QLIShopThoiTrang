# Kết quả kiểm thử xác nhận QR POS — 05/11/2026

## Cập nhật sau khi cung cấp khóa merchant

- Ba khóa được lưu ở biến môi trường **User** (`PAYOS_CLIENT_ID`, `PAYOS_API_KEY`, `PAYOS_CHECKSUM_KEY`), không ghi vào source hoặc báo cáo. API form được build, chép vào thư mục `api` cạnh WinForms và khởi động lại với cấu hình mới; người dùng cần đăng nhập lại form vì phiên API cũ đã kết thúc.
- Đã kiểm tra payOS thật bằng một link **2.000đ**, không gắn vào hóa đơn bán hàng/Supabase. SDK xác minh phản hồi tạo link, đọc trạng thái `Pending` với `AmountPaid=0`, rồi hủy ngay (`Cancelled`). Không thực hiện chuyển tiền. Bằng chứng không chứa khóa: `artifacts/qr-confirmation/merchant-config-check.json`.
- Đã sửa `HandleWebhookAsync`: sau khi kiểm chữ ký, yêu cầu `success=true`, mã kết quả ngoài và trong data đều `00`, rồi mới kiểm số tiền và hoàn tất. Ca lỗi bên dưới nay bị từ chối, không tạo hóa đơn/trừ tồn.
- Đã bỏ `CREATE TABLE/INDEX` trong `EnsureTableAsync`; API chỉ kiểm tra bảng `giaodichthanhtoanqr` có sẵn. Không chạy migration hoặc thêm bảng vào Supabase trong lượt cấu hình này.
- Build API thành công; **168 kiểm tra local PostgreSQL/HTTP qua**, gồm 17 kiểm tra QR mới. Báo cáo: `artifacts/qr-confirmation/backend-fixed-results.json`, log `artifacts/return-inspection/qr-configured-full-checks.log`.
- Chưa có xác nhận chuyển khoản thật: link thử được hủy trước khi trả tiền. Luồng ghi hóa đơn/trừ kho tiếp tục được kiểm chứng bằng webhook có chữ ký fixture trên PostgreSQL local. Chưa push Git.

Phần dưới lưu kết quả phát hiện ở lượt kiểm thử trước khi sửa.

Ở lượt kiểm thử ban đầu, chưa sửa service hoặc giao diện nghiệp vụ và chưa cấu hình payOS thật. Dữ liệu backend dùng database PostgreSQL tạm trên loopback, cùng cấu trúc 18 bảng; fixture tự xóa database khi kết thúc. Trạng thái hiện tại xem phần cập nhật phía trên.

## Luồng thành công

15 kiểm tra trình duyệt ở `tests/pos-returns/qr-confirmation-ui.mjs` qua, dùng API giả lập và ảnh QR có nhãn “QR GIẢ LẬP — KHÔNG CHUYỂN TIỀN”:

- Chọn QR, bấm thanh toán hai lần chỉ gửi một request tạo phiên, đúng tổng 108.000đ của dữ liệu mẫu.
- `PENDING` không mở hóa đơn thành công, không xóa giỏ, không trừ tồn. Lỗi provider không tự xác nhận.
- API giả lập trả `PAID` → giao diện đóng QR, mở một hóa đơn, xóa giỏ và tải tồn mới. Không POST thêm hóa đơn vì backend đã xác nhận; hai lượt poll đồng thời vẫn chỉ mở một hóa đơn.
- Hủy chưa trả tiền giữ giỏ và không tạo hóa đơn. Thiếu cấu hình merchant hiện thông báo lỗi và không đánh dấu đã trả tiền.

Backend dùng **PayOsPaymentService thật**, SDK payOS 2.1.0 thật và webhook ký HMAC bằng khóa test riêng, không dùng merchant thật. Xác minh chữ ký được kiểm tra tại chỗ qua SDK; không gọi API lấy trạng thái payOS thật:

- Chữ ký sai bị từ chối; số tiền có chữ ký hợp lệ nhưng sai mức dự kiến (thiếu/thừa 1đ) bị từ chối.
- Webhook thành công hợp lệ ghi phiên `PAID`, hóa đơn `DA_THANH_TOAN`; cả `soluong` và `soluongtonkho` từ 50 xuống 49, chi tiêu khách tăng 108.000đ.
- Gửi lại cùng xác nhận 5 lần đồng thời vẫn một hóa đơn, tồn vẫn 49, không cộng chi tiêu lặp.
- Thiếu khóa merchant bị chặn khi tạo QR; không tạo hóa đơn đã trả tiền hoặc trừ tồn.

## Lỗi phát hiện, chưa sửa

**Nhánh webhook có thể xác nhận sai payload báo thất bại.** Test gửi payload ký đúng bằng khóa fixture nhưng `success=false`, outer/data `code="01"`. Handler vẫn gọi `FinalizeAsync`: tạo một hóa đơn đã trả tiền, phiên thành `PAID` và tồn từ 49 xuống 48 (đúng ra phải giữ 49).

Vị trí: `api/QLIShopThoiTrang.Api/Services/PayOsPaymentService.cs`, `HandleWebhookAsync`. Code hiện kiểm chữ ký và số tiền, chưa kiểm cờ thành công/mã kết quả trước khi hoàn tất. Đây là lý do bộ test backend mới đang **đỏ ở ca kiểm tra lỗi này**; không được mô tả toàn bộ luồng QR đã đạt.

Quan sát giao diện sau ca thành công: nút checkout đã mở lại nhưng chữ có thể còn “Đang xử lý…”. `setCheckoutBusy(true)` lưu lại nhãn trong mỗi lần gọi; gọi lại khi đang bận ghi đè nhãn gốc. Đây là quan sát phụ, chưa sửa.

## Giới hạn và bằng chứng

- Chưa xác thực một khoản chuyển tiền thật qua ngân hàng/payOS; form hiện chưa có ba khóa merchant cần thiết trong các nguồn cấu hình local đã kiểm tra. Cấu hình payOS của web và API form khác namespace, chưa tự dùng chung.
- Chưa kiểm chứng đường tạo link và polling SDK với provider thật. Kết quả thành công ở giao diện dùng response giả; kiểm chứng ghi CSDL dùng webhook có chữ ký test hợp lệ chạy qua SDK và service thật.
- Source còn `EnsureTableAsync` chứa `CREATE TABLE IF NOT EXISTS`. Lượt test chỉ chạy phần đó trên fixture local đã có bảng; không chạy trên Supabase thật và không thêm bảng vào Supabase.
- Backend: `artifacts/qr-confirmation/backend-results.json`, log `artifacts/return-inspection/qr-confirm-checks.log`.
- UI: `artifacts/qr-confirmation/ui/results.json`, `pending-qr.png`, `confirmed-receipt.png`.
- Chạy lại backend: set `FORM_TEST_POSTGRES` tới `fashionstore_test` local, set `QR_TEST_REPORT` tới đường dẫn JSON rồi chạy `tests/version-a-checks`. Test mới được nối vào cuối bộ checks và hiện thất bại ở ca webhook failure nêu trên. Chỉ dùng kết nối test local.
