# Thanh toán QR động payOS trên WinForms

## Cấu hình trên một máy mới

1. Trên payOS, kiểm tra kênh thanh toán đang liên kết đúng tài khoản ngân hàng muốn nhận tiền.
2. Chạy file `CauHinh-PayOS.cmd` ở thư mục gốc project.
3. Nhập lần lượt `Client ID`, `API Key` và `Checksum Key` do payOS cấp.
4. Đóng hoàn toàn Visual Studio và WinForms, sau đó mở lại project và chạy form.

Ba khóa được lưu vào biến môi trường của tài khoản Windows hiện tại. Chúng không nằm trong mã nguồn và không được đẩy lên GitHub.

## Cách hoạt động

- Khi thu ngân chọn **QR**, API tạo một liên kết thanh toán payOS riêng cho hóa đơn.
- QR hiển thị đúng số tiền, nội dung chuyển khoản và tài khoản ngân hàng đã liên kết với payOS.
- Form tự kiểm tra trạng thái giao dịch. Nhân viên không có nút xác nhận thủ công.
- Chỉ khi payOS xác nhận đã nhận đủ tiền, hệ thống mới lưu hóa đơn, trừ tồn kho, ghi nhận doanh thu và mở hóa đơn để in.
- Nếu hủy trước khi thanh toán, form yêu cầu payOS hủy phiên thanh toán.

## Lưu ý bảo mật

- Không đưa ba khóa payOS vào `appsettings.json`, JavaScript, ảnh QR hoặc GitHub.
- Nếu khóa từng bị gửi công khai, nên tạo lại khóa trên payOS và chạy lại `CauHinh-PayOS.cmd` trên từng máy.
- QR tĩnh không có mã giao dịch riêng nên không dùng để tự động xác nhận hóa đơn.
- Muốn nhiều máy không cần giữ khóa riêng, cần triển khai API thanh toán lên một server trung tâm có HTTPS.
