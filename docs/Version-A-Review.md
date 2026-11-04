# Bản A bổ sung theo phản hồi review

> Tài liệu này ghi nhận bản A2 trước sửa phiên. Bản mới đã thay xmin bằng phienbanbaomat; xem [PhienBanBaoMat.md](PhienBanBaoMat.md). Migration bảo mật đã áp dụng; API cổng 5001 đã kiểm tra HTTP 200, đang chờ kiểm thử thao tác tài khoản.

Nền form: 6d11b10. Bản này bao gồm các sửa của bản A và các lỗi được phản hồi sau đó. Chưa stage/commit/push, chưa chạy trên Supabase thật.

## Bốn điểm ưu tiên đã xử lý

1. **Thêm nhanh khách hàng chỉ tạo mới.** Thu ngân/trưởng ca gọi API lưu khách hàng sẽ dùng INSERT với ON CONFLICT DO NOTHING, không đi vào nhánh cập nhật. Trùng mã, số điện thoại hoặc tài khoản được báo xung đột; bản ghi cũ không thay đổi. Hai yêu cầu đồng thời cùng mã cũng chỉ tạo được một khách. Quản lý vẫn có quyền cập nhật như trước.
2. **Phiên gắn với trạng thái nhân viên và nhóm quyền.** Mỗi yêu cầu API nội bộ đối chiếu lại tài khoản/nhóm quyền đang hoạt động, mã quyền và phiên bản dòng PostgreSQL đã ghi nhận lúc đăng nhập. Không thêm bảng/cột. Khi tắt hoặc đổi quyền qua API, các token của nhân viên bị gỡ ngay. Thay đổi trực tiếp bằng SQL được phát hiện ở yêu cầu kế tiếp, kể cả tắt/bật hoặc đổi quyền/phục hồi trước khi token cũ được dùng. Vì dùng phiên bản dòng, lưu lại hồ sơ nhân viên hoặc thay đổi bản ghi nhóm quyền cũng có thể yêu cầu đăng nhập lại. Lỗi kết nối khi kiểm tra phiên trả 503, không tự coi là tài khoản đã bị vô hiệu hóa. Thông báo hết phiên từ WebView cũ không được đăng xuất tài khoản mới.
3. **Lưu riêng theo mã nhân viên.** cart, shiftReports, checkoutAttempt, returnAttempt và các phần cache dùng khóa `y_fashion_erp_state_v2:account:<mã nhân viên>`. Không dùng tên hiển thị làm khóa và không đọc nháp của tài khoản khác. Bản lưu dùng chung cũ ở khóa gốc vẫn giữ nguyên; không suy ra chủ của nháp/báo cáo từ nhãn cache API trong bản A cũ.
4. **Đăng xuất được miễn trừ đồng nhất.** Hai lớp fetch đều cho phép `/api/dang-xuat` (kể cả dấu `/` cuối) khi dữ liệu chưa sẵn sàng. Nút đăng xuất của WinForms vẫn dọn phiên cục bộ dù API lỗi/mất kết nối. Các thao tác ghi nghiệp vụ khác vẫn bị chặn đến khi dữ liệu được xác minh lại.

## Khôi phục dữ liệu cũ, không tự gán cho người đăng nhập

- Bản lưu cũ vẫn ở nguyên khóa gốc; không xóa, không ghi đè.
- Khi có dữ liệu cũ chưa gán, form hiện thông báo riêng. Thu ngân không được tự nhận toàn bộ dữ liệu này.
- Cửa hàng trưởng chọn **Khôi phục dữ liệu cũ**, chọn rõ người nhận giỏ/mã đặt hàng, mã đổi/trả và từng báo cáo ca. Việc gán yêu cầu xác thực lại quyền quản lý bằng API.
- Chỉ chọn các nhân viên đang hoạt động. Nếu tài khoản đích đã có giỏ/mã thao tác đang dở thì báo xung đột, không ghi đè.
- Những mục chưa chọn vẫn để sau. Mục đã gán có dấu vết riêng để không được gán lại cho tài khoản khác. Bản lưu gốc vẫn nguyên vẹn sau khi khôi phục.
- Bản lưu lỗi định dạng được giữ nguyên để đối chiếu; không biến thành bản trống và ghi đè tự động.

## Những lỗi đã xác định khác

- **Đơn đang vận chuyển bị coi là hủy:** bỏ phép tìm chuỗi `%huy%`, vì nó khớp cả `chuyển`. Truy vấn và kiểm tra trạng thái dùng mã/trạng thái hủy cụ thể. Phân loại tiếng Việt không phụ thuộc cách database đổi chữ hoa/thường. Giữ đúng cả `Đã hủy`, `Đã huỷ` và các mã tương thích đang có.
- **CSV/Excel đối tác:** không còn chỉ thêm vào mảng trên trình duyệt. Cửa hàng trưởng gửi batch tới API; server kiểm tra và ghi toàn bộ trong một transaction. Một dòng lỗi thì rollback toàn bộ, không báo nhập thành công một phần. Import chỉ thêm mới, không cập nhật đối tác đã tồn tại. Danh sách chỉ đổi sau khi server xác nhận và được tải lại từ database.
- **Cài đặt:** bỏ các nút chỉ hiện thông báo giả theo yêu cầu. Không tạo module cấu hình VAT, ưu đãi thành viên hoặc thanh toán.

## Các phần tiếp tục được giữ

- Không thay đổi công thức VAT/giá/ưu đãi, logic thu tiền, thời điểm giữ/trả tồn đã đồng bộ với web hoặc định dạng mật khẩu. Các sửa tồn của bản A vẫn giữ: chỉnh thông tin SKU không ghi đè lượng tồn cũ; API trả lượng tồn thực tế.
- Thu ngân vẫn tra cứu/in lại hóa đơn POS/web khác người lập; quyền ghi không được nới thêm.
- Giữ nguồn file WebView và cách đóng gói. CORS vẫn để bản riêng, chưa coi là đã siết xong.
- Startup chỉ đọc và kiểm tra schema hiện có; không chạy DDL/DML hoặc tự suy diễn cờ của hóa đơn lịch sử. Không thêm bảng/cột và không có migration mới trong bản này.

## Kiểm thử

- Build đầy đủ WinForms net8.0-windows và API net10.0 bằng SDK 10: thành công, 0 lỗi/0 cảnh báo.
- Bộ `tests/version-a-checks`: API .NET 10 thật và PostgreSQL tạm. Kiểm tra tạo khách cũ/trùng mã đồng thời, thu hồi phiên qua API/SQL/nhóm quyền, mã có khoảng trắng, phân loại vận chuyển/hủy, CSV lưu thật/rollback, tồn kho đồng thời và startup chỉ đọc. **76 kiểm tra qua**, gồm 24 kiểm tra phân quyền/phiên.
- Bộ `tests/version-a-webview`: chính các lớp form chạy ẩn với API giả. Kiểm tra legacy không tự nhận, quản lý gán đúng người và giữ nguyên bản gốc, đổi tài khoản không nhận nháp/báo cáo/mã thao tác, đóng/mở lại, đăng xuất khi offline, CSV gửi server rồi reload, COD và các màn hình phụ. **38 kiểm tra qua**, gồm nút đăng xuất khi kết nối bị ngắt thực sự. Không gọi Supabase hoặc máy in thật.
- Bài tích hợp Native18 của source web với repository form đã chạy trên PostgreSQL tạm và qua (1/1).

Chạy kiểm thử với database riêng theo hướng dẫn hiện có của các project test. Không đặt chuỗi Supabase vào biến FORM_TEST_POSTGRES. Thử gói mới cần đóng các tiến trình form/API cũ trước; đây vẫn là bản source để team review, chưa triển khai database thật.
