# Đơn COD từ website sang WinForms

Ngày 26/10/2026. Người dùng chọn nhân viên phụ trách `NV01`.

## Phần đã triển khai

Website tạo hóa đơn trong `public.hoadon` và các dòng SKU trong `public.chitiethoadon`, với `nguondon=WEB`, `loaihoadon=COD`, phương thức COD, trạng thái `CHUA_THANH_TOAN`, giao hàng `Vừa xác nhận đơn`. Giá được tính lại từ database, không lấy tổng tiền do trình duyệt gửi. Phí giao hàng hiện 0 đồng; chưa áp dụng voucher, chiết khấu thành viên hoặc đổi điểm.

Giao dịch kiểm tra khách hàng, nhân viên đang hoạt động, SKU/danh mục đang bán và tồn kho; lưu hóa đơn, chi tiết, bản chụp thông tin mua hàng và xóa giỏ cùng một transaction. Mã yêu cầu kết hợp khách hàng tạo mã đơn ổn định; gửi lại yêu cầu cũ không tạo đơn mới. Xung đột giao dịch trả 409 để tải lại hoặc thử lại cùng mã yêu cầu.

Hai bảng `webstore.order_snapshot` và `webstore.order_line_snapshot` giữ tên, màu, size, giá và thông tin giao hàng lúc đặt. Lịch sử chỉ hiển thị đơn do phiên bản website này tạo cho đúng tài khoản. Đơn POS hoặc đơn WEB cũ chưa có snapshot chưa được nhập vào danh sách này.

Website đọc trạng thái hiện tại từ hóa đơn chung khi tải trang. Không có lịch sử từng lần chuyển trạng thái vì backend Windows chưa ghi nhật ký chung. Nếu Windows xóa hóa đơn COD, website giữ bản chụp và hiển thị “Cần liên hệ cửa hàng”, không suy đoán đã hủy/hoàn tiền và không tạo lại đơn khi retry.

## Quy tắc tồn kho và phạm vi còn thiếu

Giữ tương thích bản mã WinForms đã nhận: COD chưa trừ kho hoặc cộng điểm khi đặt; Windows xử lý khi giao thành công. Chưa có cơ chế giữ chỗ tồn kho, vì vậy đặt COD chưa đảm bảo còn hàng đến lúc xử lý giao. Website kiểm tra tồn tại lúc tạo, Windows phải kiểm tra lại khi hoàn tất.

Không tự hủy/xóa hóa đơn từ website, chưa mở chuyển khoản, ưu đãi, yêu cầu đổi trả hoặc bình luận trong bước này. Các chức năng đó cần đợt triển khai tiếp theo.

Báo cáo nhóm đang mô tả trừ kho ở “Sẵn sàng giao”, khác với WinForms. Lựa chọn tương thích hiện tại cần được phản ánh lại vào đặc tả hoặc thay đổi đồng bộ cả hai backend; không chỉ sửa website.

## Điều kiện tích hợp với partner

Mã `StoreRepository.cs` đã nhận dùng transaction mặc định cho tạo hóa đơn, giao hàng và xóa COD. Khi nhiều yêu cầu hoàn tất cùng lúc, có nguy cơ đọc trạng thái/tồn cũ rồi trừ kho/cộng điểm lặp.

Đã chuẩn bị `docs/WinForms-Order-Concurrency.patch` để đổi ba transaction này sang Serializable. Bản vá chưa áp dụng vào thư mục partner và chưa biên dịch/kiểm thử ứng dụng Windows. Partner cần rà soát và áp dụng trên phiên bản hiện hành; xử lý PostgreSQL 40001/40P01 bằng thông báo xung đột và tải lại trạng thái, không báo thành công. Bản vá này không bổ sung giữ chỗ và không thay thế kiểm thử tích hợp toàn bộ nhập/xuất/đổi trả.

Trước khi dùng chung ngoài thử nghiệm, kiểm thử đồng thời hai yêu cầu giao thành công cho cùng đơn và POS/Web bán SKU cuối: chỉ một lần trừ kho/cộng điểm, không tồn âm. Không coi kết quả kiểm thử website là đã xác minh WinForms.

## Cách chạy thử tích hợp

1. Xác nhận `NV01` tồn tại và đang hoạt động trong Supabase. Backend kiểm tra lại mỗi lần tạo đơn.
2. Nếu chưa áp dụng bước 2, chạy `database/Supabase-Customer-Cart.sql` trong SQL Editor.
3. Đọc và chạy `database/Supabase-Web-Orders.sql` trong SQL Editor bằng chủ database. Script chỉ bổ sung các bảng snapshot, kiểm tra cột cần thiết; không thay đổi hàm/trigger chung hoặc chạy initializer của partner. Cần rà soát trigger đang có nếu database thật khác bản source.
4. Dừng backend rồi chạy:

```powershell
.\scripts\Start-SupabaseCatalog.ps1 -EnableOrders -OrderEmployeeId NV01
```

`-EnableOrders` tự bật tài khoản và giỏ hàng. Chạy không có switch này sẽ khóa đơn hàng. Cấu hình tương ứng: `Database__EnableSupabaseOrders=true`, `Database__EnableSupabaseCustomerFeatures=true`, `Orders__EmployeeId=NV01`.

5. Tạo một đơn COD thử với sản phẩm thử nghiệm. Đối chiếu mã đơn, nguồn WEB, nhân viên NV01, khách hàng, SKU và tổng tiền bên Windows.
6. Cập nhật giao hàng qua Windows rồi tải lại chi tiết đơn trên website. Kiểm tra tồn kho/điểm trước và sau giao thành công, kiểm tra lặp thao tác. Dùng dữ liệu thử rõ ràng vì hoàn tất trên Windows sẽ ghi tồn kho/điểm thật.

## Kết quả kiểm thử hiện tại

PostgreSQL riêng trên máy cục bộ: migration chạy lại được; chặn nhân viên ngừng hoạt động, thiếu tồn, chuyển khoản và voucher chưa hỗ trợ; tạo đơn nguyên tử; gửi đồng thời/retry không trùng; giỏ còn nguyên khi thất bại; giá mới được tính lại; quyền xem theo chủ tài khoản; snapshot không đổi khi sản phẩm đổi tên/giá; đọc trạng thái hóa đơn cập nhật; giữ đơn tham chiếu khi hóa đơn bị xóa. Cập nhật trạng thái trong bài kiểm thử được mô phỏng bằng SQL, không phải chạy WinForms.

Bộ kiểm thử đầy đủ: 80 thành công, 15 SQL Server bỏ qua. MVC/Razor build thành công. Một lần chạy trong sandbox bị timeout kết nối testhost; lần chạy đầy đủ với quyền phù hợp đã thành công. Chưa chạy SQL hay tạo đơn trên Supabase chung và chưa kiểm tra giao diện trực quan bằng trình duyệt ở đợt này.
