# Kết quả kiểm thử — Account/Cart, 16/10/2026

**Bản hiện tại: 47 PASS, 0 FAILED, 15 SQL SKIP (62 tổng).** Product API bản trước đã xác nhận riêng bằng TRX Windows: **31/31 PASS**. Không cộng kết quả hai phiên bản để tuyên bố toàn bộ bản mới PASS.

| Kiểm tra | Trạng thái | Bằng chứng / giới hạn |
|---|---|---|
| Product API bản trước trên Windows | PASS | evidence/ProductApi_Windows_31_Passed.trx: 31 bản ghi Passed, gồm bảy SQL test. Đã đọc file, không chỉ dựa vào terminal. |
| API Release bản hiện tại | PASS | Build từ lần chạy test, không có warning/error. |
| C# MVC target net48 | PASS | evidence/account-cart-mvc-build.log: 0 warning/error; chưa compile Razor/IIS. |
| 24 test nền tảng/catalog/client/SQL translation cũ không cần engine | PASS | evidence/AccountCart_Local.trx, giữ coverage của Product API. |
| 12 HTTP/security test mới | PASS | Login/session expiry/logout/password version, account/customer bị khóa, ownership, payload thừa, signup hash và rate limit. Dùng EF InMemory cho I/O test, không xác minh constraint/transaction SQL. |
| 8 MVC client contract test mới | PASS | Per-request bearer không lẫn khách, không gửi id/điểm/hạng, payload SKU/quantity, URI encoding, lỗi API/contract sai không biến thành giỏ rỗng. |
| Mapping + cart SQL translation mới | PASS | Hai test: toàn bộ cột/PK đã map so với export thật; câu đọc giỏ có owner predicate, giá SKU hiện tại và sort được provider SQL Server dịch. Chưa execute SQL ở đây. |
| Public endpoints khi chưa có config SQL | PASS | Live/Swagger vẫn 200; private challenge 401; ready 503. |
| Kestrel HTTP thật | PASS | evidence/account-cart-runtime.json: 6 HTTP check đúng status và Swagger 14 path đúng public/private auth metadata. DB cố ý chưa cấu hình. |
| 7 SQL catalog regression trên bản mới | SKIP/BLOCKED tại môi trường này | Cần chạy lại cùng source mới; TRX Windows bản trước đã chứng minh bảy test đó PASS ở checkpoint trước. |
| 8 SQL Account/Cart mới | SKIP/BLOCKED | Có test atomic signup/duplicate/race, rowversion, ownership, PUT/DELETE lặp, collation SKU, stock, concurrent adds, giá/tồn thay đổi và đổi mật khẩu. Chưa có engine SQL tại đây. |
| Razor/IIS/UAT Account/Cart mới | NOT TESTED | C# compile không thay thế render Razor, cookie/anti-forgery và trình duyệt thực trên Windows. Có hướng dẫn MSBuild và checklist ở ACCOUNT_CART.md. |
| Export metadata DEV | PASS theo file người dùng cung cấp | 29 bảng/2 view/47 FK/6 SP, không có disabled/untrusted constraints hoặc module definition thiếu. Chỉ xác minh metadata và mapping, không audit dữ liệu nghiệp vụ hoặc so sánh YodyStore/backup. |
| Checkout/Order/Loyalty/Return/Review/Wishlist | Chưa triển khai | Không có endpoint mô phỏng thành công. |

## Bằng chứng

- evidence/AccountCart_Local.trx và account-cart-tests.log: kết quả tự động mới. Đọc outcome từng UnitTestResult để đếm SKIP, vì Counter có thể không phản ánh đúng skip của xUnit.
- evidence/ProductApi_Windows_31_Passed.trx: TRX người dùng gửi, giữ nguyên byte.
- database/schema-reference/FashionStoreDB_Dev_schema_20261015_170000_433_bf029ba2.json: metadata người dùng gửi, giữ nguyên byte.
- evidence/account-cart-runtime.json, account-cart-runtime.log: Kestrel thật không có SQL config.
- evidence/account-cart-mvc-build.log, docs/openapi.v1.json, evidence/verification.json.
- Log/TRX Product API ban đầu, kiểm tra Roslyn và PowerShell vẫn giữ để truy nguyên các checkpoint trước; không dùng làm bằng chứng chạy mới.

## Nghiệm thu Windows

Theo docs/ACCOUNT_CART.md, chạy script Test-SqlCatalog.ps1 trên FashionStoreDB_Dev. Mục tiêu **62 passed, 0 failed, 0 skipped**, rồi kiểm tra các trang đăng ký/login/profile/password/cart, hai tài khoản độc lập, quantity và stock, logout và form anti-forgery trên IIS Express. Không tạo/migrate database hoặc sửa procedure khi chạy module này.
