# Bản form tích hợp tương thích web và giữ chức năng main

Nền main: 6d11b106b7025fe52efd7e25f73c7ec6efc39dd6. Nền tương thích web: fd6c6060daf135f6e261a24057e042d54fd2d021.

## Phạm vi

Giữ màn hình, nút nghiệp vụ, API và 261 hàm JS của main; giữ các sửa lỗi tồn kho, đơn hủy, CSV lưu thật, phân quyền, phiên đăng nhập và bản nháp theo tài khoản của nhánh phụ. Nút Cài đặt chỉ hiện thông báo giả không được khôi phục. Ô tồn khi sửa SKU vẫn chỉ đọc theo quy tắc đã chọn.

Bổ sung nhận diện staffSecurityVersion=1 khi form kiểm tra API: không dùng lại API cũ chỉ vì cổng 5001 còn trả health thành công. Các xử lý giá/VAT/QR/đổi trả và tài nguyên giao diện của main được giữ nguyên.

## Kiểm chứng

- Build WinForms net8.0-windows và API net10.0: 0 lỗi, 0 cảnh báo.
- Đối chiếu tĩnh: 899/899 điểm đạt, bao gồm ID DOM, nút xử lý, điều hướng, hàm JS và API.
- API thực trên PostgreSQL localhost, dữ liệu giả: 105 kiểm tra đạt. 29 kiểm tra phân quyền nằm trong tổng 105.
- WinForms/WebView2 thực, API giả: 46 kiểm tra đạt, gồm bốn bài nhận diện API cũ/mới.
- Chỉ đọc Supabase hiện tại: đủ hai cột UUID, hai trigger bảo mật; 18 bảng public. API mới khởi động được và GET health trả thành công tại cổng tạm 5031, sau đó đã dừng.
- Không chạy migration hoặc tạo/sửa dữ liệu bán hàng trên Supabase trong lần tích hợp này.

Các lượt đầu của bộ test gặp timeout khởi động/cleanup localhost và WebView chưa sẵn sàng. Đã tăng thời gian chờ khởi động và bổ sung chẩn đoán; không thay điều kiện kiểm tra nghiệp vụ. Log thất bại được giữ bên cạnh log chạy lại đạt trong artifacts/main-parity.

Đối chiếu tĩnh không thay thế kiểm thử mọi tổ hợp dữ liệu. Website trên máy khác chưa được chạy xuyên suốt trong phiên này; các thay đổi tương thích của nhánh đối tác được giữ. Báo cáo ca vẫn lưu trên từng máy, chưa có đồng bộ database.

## Mở đúng project

Mở winform/qlicuahang/qlicuahang.csproj trong repository QLIShopThoiTrang, chọn Startup Project, build rồi chạy. Không mở bản sao cũ ở KLCN/qlicuahang để tránh chạy nhầm phiên bản.

Sửa đổi cũ đã được sao lưu và kiểm tra hash tại C:/Users/Anh Vu/OneDrive/Desktop/KLCN/qlicuahang/main-parity-backup-20261104; Git stash cũng giữ bản tương ứng. Không áp lại nguyên stash vì các sửa lỗi đã có trong bản tích hợp.

Cây Git website của main và bản tích hợp cùng hash 16fe2099580ee0e473e4f8942f1d5c5de831a6fc. 262 file website bị xóa trong working tree đã có từ trước khi tích hợp, không được stage/commit trong công việc này.

## Chạy kiểm tra lại

Chạy node tests/main-parity/verify.mjs từ gốc repository để đối chiếu main. Các project kiểm thử nằm ở tests/version-a-checks và tests/version-a-webview. Bài database chỉ chấp nhận FORM_TEST_POSTGRES trỏ localhost, Database=fashionstore_test; tự tạo/xóa database tạm riêng. Không đặt chuỗi Supabase vào biến này. Cổng 5001 cần trống cho API giả của bài WebView.
