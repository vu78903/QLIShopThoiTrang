# Trạng thái đang tải / xử lý của form

Các màn hình dùng `thao-tac-an-toan.js` hiện một thông báo chung có vòng xoay khi tải dữ liệu hoặc gửi thay đổi đến API. Nội dung phía sau được đặt `inert`, chặn click/Enter/phím tắt và gửi tín hiệu bận cho WebView để hạn chế điều hướng do người dùng trong lúc chờ. Nội dung thông báo phân biệt **Đang tải dữ liệu…** và **Đang xử lý yêu cầu…**.

- Chỉ mở lại sau khi tất cả request đang chờ đã đọc xong nội dung phản hồi. Các request nối tiếp dùng cùng trạng thái chờ; hoàn tất một request không mở khóa khi request khác còn chạy.
- Không thay trạng thái `disabled` của các nút bị khóa theo quyền hoặc theo nghiệp vụ. Khi mở lại, giữ dữ liệu người dùng đã nhập và phục hồi ô đang chọn nếu còn tồn tại.
- Lỗi HTTP, lỗi mạng, hủy request và timeout đều giải phóng khóa trong `finally`. GET có giới hạn 30 giây, ghi có giới hạn 60 giây; request vốn có thời hạn ngắn hơn vẫn giữ thời hạn đó. Timeout không khẳng định giao dịch ở server đã thất bại; người dùng cần tải lại kiểm tra kết quả trước khi gửi lại.
- Giữ cơ chế chống bấm lặp và gộp request ghi giống nhau. Request lỗi giữ mã `X-Request-ID` cho lần thử lại. Không tự gửi lại giao dịch.
- Chờ người dùng xác nhận chưa bật vòng xoay; hộp xác nhận vẫn thao tác được. GET tự kiểm tra QR nền không khóa màn hình thanh toán liên tục. Đăng xuất giữ cách xử lý riêng hiện có.
- Đăng nhập có vòng xoay tại nút, khóa tài khoản/mật khẩu/nút hiện mật khẩu và chặn gửi lần hai. C# giới hạn 30 giây; lỗi mở lại các ô và giữ dữ liệu để sửa.
- Điều hướng tự động theo vai trò vẫn được phép; khóa điều hướng do người dùng không chặn chuyển đúng trang sau khi đăng nhập.

Không sửa database/API nghiệp vụ, không chạy SQL và không commit/push trong lượt sửa này. Đã build bản chính ở `winform/qlicuahang/bin/Debug/net8.0-windows/`; mở lại form để nhận assembly và asset mới.

## Kiểm chứng local 05/11/2026

- Build WinForms thành công: 0 lỗi, 0 cảnh báo, log `artifacts/return-inspection/busy-main-build.log`.
- 53 kiểm tra WinForms/WebView thật qua, gồm tải chậm có spinner, chặn điều hướng bằng click đang xếp hàng, mở lại đúng quyền và các kiểm tra phiên/dữ liệu cũ. Fixture dùng cổng ngẫu nhiên và chặn request trong WebView sang API giả, không chiếm cổng 5001 hoặc gửi giao dịch test vào API/Supabase thật.
- `tests/pos-returns/busy-ui.mjs`: 23 kiểm tra qua về tải chậm, request đồng thời, khóa cả nội dung thêm mới trong lúc chờ, bản nháp/focus, chống gửi lặp, HTTP/network/timeout, mã gửi lại, QR nền, xác nhận và đăng nhập. Ảnh/báo cáo tại `artifacts/busy-ui/browser/`.
- 22 kiểm tra UI kiểm nhận và 27 kiểm tra UI hồi quy qua. Không có lỗi JavaScript trong các ca trên.
