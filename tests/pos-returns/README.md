# Kiểm thử lỗi POS và đổi trả 1–5

Chạy tại thư mục gốc repository. Cần .NET 10, biến môi trường kết nối Supabase và Edge/Playwright đã có trong WinForms. Chỉ chạy trên môi trường được cho phép kiểm thử. Fixture dùng đúng tiền tố TESTPOS/TSTPOS và hai tài khoản TESTFIX, không dùng tài khoản thật.

1. Build API, chạy bản mới tại cổng thử 5022 để initializer cập nhật cột trước khi tạo yêu cầu web:
   `dotnet run --project api/QLIShopThoiTrang.Api -- --urls http://127.0.0.1:5022`
2. Tạo fixture: `dotnet run --project tests/pos-returns/db -- tests/pos-returns/testpos-setup.sql`
3. Chạy: `node tests/pos-returns/api.mjs` và `node tests/pos-returns/ui.mjs`.
4. Luôn dọn fixture, kể cả test thất bại:
   `dotnet run --project tests/pos-returns/db -- tests/pos-returns/testpos-cleanup.sql`

API test tạo chứng từ thật cho dữ liệu thử, bao gồm yêu cầu web nhiều SKU bằng bảng tiếng Việt. UI test chặn API và chỉ dùng dữ liệu giả. Kết quả và ảnh nằm trong `artifacts/pos-returns/`, được bỏ qua bởi Git. Dừng API thử sau khi xong.

Không gọi file setup lần hai khi chưa cleanup. Nếu ID fixture đã tồn tại, dừng và kiểm tra trước khi dọn; không thay mã để ghi đè dữ liệu khác.

## Kiểm thử thêm mục 6–9

Sau setup gốc, chạy thêm `fix69-setup.sql` bằng cùng công cụ DB. Cần build công cụ DB trước khi chạy các script Node có SQL (`dotnet build tests/pos-returns/db`).

Chạy lần lượt `api.mjs`, `fix69-api.mjs`, `fix69-real-ui.mjs` trên API thử 5022. Hai script cuối dùng các SKU/tài khoản TEST69 riêng; script UI thật ghi dữ liệu qua form vào API thử/Supabase. `fix69-real-ui.mjs` chạy sau `fix69-api.mjs` vì kiểm tra tài khoản fixture đã được cập nhật.

`ui.mjs` và `fix69-ui.mjs` dùng mock API, có thể chạy độc lập. Script mới kiểm tra cả bộ lọc, kiểm nhận hàng thực tế, nút Hiện/Ẩn mật khẩu, khóa lưu và dialog trên cùng ở các màn hình.

Luôn chạy cleanup theo thứ tự **fix69-cleanup.sql rồi testpos-cleanup.sql**, kể cả khi test lỗi. Không chạy lại fixture khi chưa cleanup. Nếu chạy lại toàn bộ, khởi động lại API thử để tránh cache X-Request-ID của lượt trước. Dừng riêng tiến trình API thử khi hoàn tất.

## Hạng thẻ và voucher

Chính sách hiện tại chọn mức giảm tốt hơn giữa thành viên và voucher, không cộng dồn; nếu bằng nhau ưu tiên thành viên. Các kỳ vọng trong bộ kiểm thử POS đã được cập nhật theo quy tắc này.

`membership-ui.mjs` dùng mock và có thể chạy độc lập. `membership-api.mjs` chạy trên API thử 5022 sau setup gốc, hoặc sau `api.mjs`; script chỉ thay đổi tổng chi tiêu của khách TESTPOS và tạo hóa đơn có tiền tố TESTPOS. Chạy cleanup gốc khi xong. Không chạy `api.mjs` sau script membership trên cùng fixture chưa reset vì ngưỡng chi tiêu đã được thay đổi để kiểm tra các mốc.

## Bật/tắt SKU và lưu sản phẩm

`node tests/pos-returns/product-actions-ui.mjs` kiểm tra cả handler khóa nút riêng và wrapper khóa dùng chung: tắt/bật SKU, bấm lặp, mở khóa sau lỗi và lưu nhóm sản phẩm. Dùng mock, không đổi trạng thái hàng thật.

Thêm `--before` để tái hiện lỗi tại commit `df74242` (cần có commit này trong lịch sử Git). Lượt trước sửa dự kiến thất bại vì handler thấy nút bị wrapper khóa nên không gửi API. Bản sửa giữ khóa logic ngay từ đầu nhưng để handler đọc trạng thái nút ban đầu trước khi khóa giao diện.
