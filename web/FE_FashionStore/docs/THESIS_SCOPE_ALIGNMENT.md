# Căn cứ đề tài và kế hoạch tích hợp Supabase

Ngày đối chiếu nội dung: 26/10/2026. Đây là ghi chú triển khai, không thay thế báo cáo của nhóm hoặc xác nhận của GVHD.

## Tài liệu nguồn

- Đề cương GVHD: `C:\Users\KIET\Downloads\CNTT-KLCN084_ManhThienLy.pdf` (8 trang). Mục tiêu và chức năng tại trang 1–4; kế hoạch tại trang 5–7.
- Báo cáo nhóm: `C:\Users\KIET\Downloads\KLCN-084_Tuan11.docx`. Đã đọc nội dung văn bản và bảng; chưa thẩm định các sơ đồ nhúng dưới dạng hình.
- Người dùng xác định PDF là hướng đi chính của GV và Word là nội dung của nhóm. Dùng PDF làm căn cứ phạm vi, Word làm đặc tả triển khai; điểm mâu thuẫn cần đối chiếu trước khi áp dụng nghiệp vụ.

## Phạm vi website theo đề cương

1. Giới thiệu cửa hàng, danh mục và thông tin sản phẩm.
2. Tìm kiếm, lọc và lựa chọn sản phẩm theo nhiều tiêu chí.
3. Thành viên, nhận thông tin khuyến mãi, ưu đãi và tích điểm.
4. Đặt hàng, thanh toán, theo dõi đơn và lịch sử mua hàng.
5. Phản hồi đổi trả và bình luận sản phẩm.
6. API, kiểm thử, triển khai và tích hợp Web + Windows.

Không coi kết nối database thành công hoặc giỏ hàng chạy được là hoàn thành toàn bộ phạm vi. Đề cương bố trí đồng bộ Web + Win ở tuần 9, kiểm thử/hoàn thiện tuần 10–11 và hoàn chỉnh báo cáo tuần 12.

## Công nghệ nhóm đã ghi nhận

Bảng 4.1 mô tả MVC5/.NET Framework 4.8 cho website, ASP.NET Core/.NET8/EF Core8 cho backend website, WinForms/.NET8/WebView2 cho ứng dụng Windows và ASP.NET Core/.NET10/Npgsql cho backend Windows. PostgreSQL trên Supabase là nguồn dữ liệu chung; Storage lưu ảnh. Không suy luận rằng đã hoàn thành tích hợp chỉ từ bảng công nghệ.

## Trạng thái triển khai đã kiểm chứng trong phiên làm việc

- Catalog và chi tiết SKU: người dùng đã xác nhận đọc được từ Supabase.
- Tài khoản và giỏ hàng: code và SQL bổ sung đã chuẩn bị, kiểm thử trên PostgreSQL riêng; chưa có xác nhận áp dụng SQL và kiểm thử trên Supabase chung.
- Đơn hàng/thanh toán và các chức năng còn lại: chưa hoàn tất chuyển Supabase. Checkout hiện khóa trong chế độ này.
- Cập nhật tiếp ngày 26/10: đã bổ sung luồng COD và đọc trạng thái đơn với switch riêng `-EnableOrders`, nhân viên người dùng chọn `NV01`. Xem `SUPABASE_WEB_ORDERS_SETUP.md`; kiểm thử PostgreSQL riêng đã đạt, chưa bật trên Supabase chung hoặc xác minh tích hợp Windows. Nhận định checkout khóa ở trên áp dụng chế độ mặc định không có switch.
- 80 kiểm thử thành công và 15 kiểm thử SQL Server bỏ qua không chứng minh toàn bộ hệ thống Web + Windows đã tích hợp thành công.

## Những điểm cần thống nhất trước khi chuyển đơn hàng

- Word mô tả COD tạm giữ hàng; quy trình giao hàng trừ tồn ở trạng thái “Sẵn sàng giao”. Mã WinForms đã khảo sát trước đó trừ tồn COD khi giao thành công. Phải chọn một quy tắc chung, bảo đảm mỗi đơn chỉ trừ/hoàn tồn một lần; không để website tự áp dụng quy tắc khác.
- Word mô tả website quản trị ở phần công nghệ, nhưng chương cài đặt và giao diện lại nêu website phục vụ khách mua hàng. Đề cương yêu cầu website bán hàng và phần mềm Windows quản lý. Ưu tiên hoàn thành luồng khách hàng; xác định rõ phạm vi quản trị web trước khi mở rộng.
- Word ghi tích 1 điểm/10.000 đồng, quy đổi 1 điểm/1.000 đồng, hạng Bạc/Vàng/Kim Cương và ngưỡng chi tiêu. Cần đối chiếu logic WinForms, cách làm tròn, thời điểm cộng/hoàn điểm và cộng dồn ưu đãi trước khi dùng trong thanh toán.
- Word ghi đổi trả 15 ngày, có loại hàng ngoại lệ và kiểm tra tình trạng hàng. Website tiếp nhận yêu cầu không đồng nghĩa tự phê duyệt hoặc tự hoàn tồn.
- Tự tạo vận đơn và đối soát đơn vị giao hàng được mô tả trong Word, nhưng chưa có bằng chứng tích hợp dịch vụ thật. Cần phân biệt thao tác nội bộ với kết nối bên ngoài.

## Thứ tự triển khai đề xuất

1. Xác nhận tài khoản/giỏ hàng hoạt động với schema Supabase chung.
2. Đối chiếu hợp đồng dữ liệu đơn hàng với WinForms: mã đơn, nguồn WEB, khách hàng, nhân viên phụ trách, SKU, tiền hàng/ưu đãi/phí giao, thanh toán, trạng thái và tồn kho.
3. Tạo đơn, thanh toán theo phương thức thực sự được hỗ trợ, lịch sử và theo dõi đơn; kiểm thử đơn WEB xuất hiện bên Windows và cập nhật ngược lại.
4. Hoàn thiện khuyến mãi, tích điểm/thành viên theo quy tắc chung.
5. Bổ sung phản hồi đổi trả, bình luận sản phẩm và các luồng tài khoản còn thiếu.
6. Kiểm thử tích hợp và ghi kết quả thực tế vào TC-01 đến TC-15; cập nhật báo cáo theo phần mềm đã chạy được.

## Các chỗ trong Word cần chỉnh sau

- Phần kiểm thử còn nội dung “Tìm kiếm bánh”, “loại bánh”, “thành phần”.
- Bảng nghiệp vụ nhập hàng có dòng thay thế thuộc xử lý hàng lỗi.
- UC-07 bán hàng có mô tả tổng quan thuộc nhập hàng.
- Nhiều hình/caption còn chỗ trống; kết quả kiểm thử còn để trống hoặc “Chưa kiểm thử”. Không tự điền đạt khi chưa có bằng chứng.
- Cần bổ sung/đối chiếu đặc tả luồng khách hàng, nhất là theo dõi đơn, bình luận và đổi trả, với yêu cầu PDF.

Hai file gốc được giữ nguyên; các ghi chú trên chưa phải chỉnh sửa báo cáo.
