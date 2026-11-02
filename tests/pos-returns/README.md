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
