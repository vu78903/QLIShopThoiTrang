# Báo cáo kiểm tra Task 3 — 2026-10-19

## Kết luận

Chưa đủ điều kiện nghiệm thu toàn bộ Task 3. Đã triển khai source MVC/API và kiểm tra tĩnh; **chưa build C#, chưa compile Razor, chưa chạy SQL, chưa chạy trình duyệt trên website thật**. Không lấy kết quả 62/62 cũ làm bằng chứng cho bản này.

| Hạng mục | Kết quả | Bằng chứng / giới hạn |
|---|---|---|
| JS syntax | PASS | node --check Scripts/moc-nhien.js, exit 0 |
| MVC source/view/assets trong csproj | PASS | Không thiếu, không trùng, đường dẫn tồn tại |
| XML configs/project | PASS | Parse 10 file XML |
| Admin-specific source và procedure script tồn kho | PASS | Hash bằng bản gốc; shared OrderService có mở rộng và vẫn cần regression |
| Metadata CheckoutRequest | PASS (review) | param: validation giữ nguyên |
| Nút placeholder / Html.Raw review | PASS (static) | Không phát hiện trong Customer views; không phải test XSS runtime |
| Backend build | SKIP / BLOCKED | Đã thử dotnet build: dotnet: command not found |
| MVC build và Razor | SKIP | Không có VS/MSBuild/.NET Framework/IIS Express |
| xUnit gốc + CustomerTask3Tests | SKIP | Không có .NET SDK. Đã bổ sung 10 test case (validation/filter translation/discount/anonymous access), chưa chạy |
| SQL migration, giao dịch, constraints | SKIP | Không kết nối được máy SQL Server của người dùng trong môi trường này |
| Responsive 1920/1366/768/375 | SKIP | Có CSS breakpoint; chưa render website thật nên chưa xác nhận chất lượng trực quan |
| SMTP, expiry, reuse token | SKIP | Không có cấu hình SMTP thực để thử |

Chi tiết kiểm tra tĩnh: CUSTOMER_STATIC_CHECKS.json. PASS tĩnh chỉ phản ánh điều được kiểm tra, không chứng minh chức năng chạy đúng.

## Checklist nghiệm thu bắt buộc trên DEV

Tất cả các dòng dưới hiện **SKIP — cần Windows/IIS/SQL Server DEV**.

1. Flow mua hàng: đăng ký → login → chọn SKU → giỏ → checkout COD → success → xem đơn; refresh form không tạo đơn trùng; không trừ kho lúc tạo.
2. Flow Admin: đăng nhập chung vào đúng Dashboard → xác nhận đơn → Customer thấy timeline và tồn kho giảm đúng một lần.
3. Flow hủy: đúng chủ hủy đơn mới/xác nhận chưa giao; tồn hoàn đúng một lần; lặp yêu cầu không cộng kho lại; khách B không hủy đơn A; đơn đã trả tiền/đang giao bị chặn.
4. Voucher: đủ/thiếu tối thiểu, hết hạn, hết lượt, mã sai, hai đơn tranh lượt cuối; rollback không tăng lượt; retry RequestId không tăng lần hai; số tiền trong ThanhToan bằng tổng sau giảm. Kiểm tra chặn giảm hết 100% do CHECK SoTien>0.
5. Thành viên: ledger và số dư trùng dữ liệu thực; hoàn tất/hoàn trả bên Windows cập nhật điểm rồi refresh Web. Web không thực hiện cộng/đổi điểm mới.
6. Đổi trả: đơn hợp lệ trong 15 ngày; quá hạn, thiếu mốc giao, khác chủ, SKU ngoài đơn, vượt số lượng, yêu cầu đồng thời và retry GUID; đổi SKU hết hàng; trạng thái CHO_DUYET, chưa thay kho/tiền/điểm.
7. Đánh giá: chỉ đơn đã hoàn tất đúng chủ, chống trùng; nội dung HTML hiển thị dạng chữ; chỉ HIEN_THI public, CHO_DUYET chưa public.
8. Phân quyền: Customer không vào Admin; Admin cookie không thành Customer; bearer Admin không dùng endpoint Customer; địa chỉ/wishlist/points/history không lộ dữ liệu tài khoản khác.
9. Reset: chưa config trả 503 với mọi email; email không tồn tại không khác thông báo; email thật nhận link; token hết hạn/dùng lại/đã đổi mật khẩu không hợp lệ; sau reset session cũ mất hiệu lực.
10. Wishlist/addresses: lưu lại sau logout/login; xóa chỉ đúng chủ; chọn địa chỉ áp dụng đúng checkout; sản phẩm ngừng bán có trạng thái phù hợp.
11. UI: duyệt mọi route, bàn phím/focus, mobile menu/search Escape, ảnh fallback, empty/error/loading, trang thứ hai và back navigation. Không dùng screenshot mockup thay cho nghiệm thu website.

## Phần chưa hoàn thành / cần quyết định

- Quy đổi điểm thành tiền, ngưỡng hạng, tiến trình hoàn tất/cộng-điều chỉnh điểm mới: chưa có đặc tả đủ, không tự thêm. Chỉ tích hợp hiển thị điểm/ledger.
- Upload ảnh/video minh chứng đổi trả chưa triển khai; form tiếp nhận mô tả.
- Duyệt review/xử lý đổi trả chưa có UI Admin mới (ngoài phạm vi sửa Customer); cần hệ thống Windows/Admin thực hiện.
- Chưa có mua lại một lần bấm; khách chọn lại sản phẩm trong catalog. Wishlist dẫn tới chi tiết chọn SKU trước khi thêm giỏ.
- Bảng size số đo, chất liệu/chăm sóc từng sản phẩm, bán chạy chưa có dữ liệu tương ứng; không tạo dữ liệu giả.
- Không có voucher cấp riêng cho tài khoản; danh sách là chương trình hợp lệ chung.
- Sổ địa chỉ hỗ trợ thêm/xóa/chọn, chưa chỉnh sửa trực tiếp một bản ghi (có thể thêm bản đúng rồi xóa bản cũ).
- Chưa có test SQL integration tự động mới cho các transaction; các case trên phải chạy trước merge.
- Cần chốt hoàn lượt voucher khi hủy/đổi trả; bản này dùng lượt khi tạo đơn và không sửa stored procedure cũ.
- Thông tin cửa hàng/ngân hàng và SMTP cần cấu hình thật. Ảnh trang chủ bên ngoài cần kiểm tra.
