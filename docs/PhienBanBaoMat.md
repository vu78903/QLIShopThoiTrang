# Đề xuất sửa quy tắc duy trì phiên nhân viên

## Trạng thái

Người dùng đã đồng ý bổ sung hai cột và trigger. Bản sửa đã ghép vào source form và build thành công, 0 lỗi/0 cảnh báo. **Migration đã áp dụng trên Supabase thật**, xác nhận qua `live-result.json`: đủ 18 bảng, hồ sơ/tài khoản/quyền giữ nguyên, schema/trigger hợp lệ. API mới cổng 5001 đã khởi động và kiểm tra lại `/api/health` trả HTTP 200. Chưa stage/commit/push form. Kiểm thử thao tác giữ/thu hồi phiên bằng tài khoản thật đang chờ người dùng thực hiện; không coi health check là bằng chứng chức năng đã qua trên Supabase.

Nền chuẩn bị là source form tại HEAD `6d11b106b7025fe52efd7e25f73c7ec6efc39dd6` kèm thay đổi A2/A2.1 và sửa bước chờ của bộ kiểm thử. `baseline.json` lưu hash từng file nguồn. Khi ghép vào source partner, phải đối chiếu từng phần thay vì chép đè cả file.

## Quy tắc

- Sửa tên, điện thoại, email, địa chỉ, nhãn chức vụ hoặc tên hiển thị nhóm quyền: giữ phiên.
- Gửi lại tài khoản/mật khẩu hiện có mà giá trị không đổi: giữ phiên.
- Đổi tên đăng nhập, mật khẩu, mã quyền hoặc trạng thái hoạt động: vô hiệu thế hệ phiên cũ.
- Tắt rồi bật lại, hoặc đổi quyền rồi phục hồi trước khi token cũ được dùng: token cũ vẫn không được khôi phục.
- Request của token cũ chỉ loại token đó. Thao tác quản trị chỉ gỡ thế hệ phiên trước cập nhật, không gỡ phiên mới được tạo sau cập nhật.
- Định dạng lưu và kiểm tra mật khẩu giữ nguyên cơ chế hiện tại, không xử lý lại vấn đề tương thích mật khẩu web/form trong bản này.

## Thay đổi database cần team chốt

SQL `BoSung-PhienBanBaoMat.sql` bổ sung:

| Bảng hiện có | Cột | Kiểu |
| --- | --- | --- |
| `public.nhanvien` | `phienbanbaomat` | `uuid NOT NULL` |
| `public.phanquyen` | `phienbanbaomat` | `uuid NOT NULL` |

Kèm hai hàm và hai trigger tiếng Việt. Trigger cấp dấu phiên bản mới khi tạo bản ghi hoặc đổi các trường bảo mật; cập nhật hồ sơ thông thường giữ nguyên dấu phiên bản. Chỉ số `xmin` của toàn dòng không còn được dùng để quyết định hiệu lực phiên.

Dùng UUID thay cho số khởi tạo lại từ 1 để việc xóa rồi tạo lại bản ghi không tái sử dụng thế hệ cũ. Script chạy trong transaction, giữ thông tin hồ sơ/tài khoản/quyền, chỉ điền dấu bảo mật cho dòng chưa có. Chạy lại không đổi dấu đã thiết lập. Không thêm bảng: kiểm thử vẫn có đúng 18 bảng.

Đây là thay đổi cấu trúc dùng chung nên cần team đồng ý hai cột và trigger trước khi áp dụng. API mới chỉ khởi động nếu cột và trigger đang hoạt động có đủ; startup vẫn chỉ đọc, không tự tạo đối tượng database.

Xác nhận bổ sung đã nhận từ người dùng. Bước còn lại là nhập kết nối kín và chạy công cụ áp dụng, không phải yêu cầu thêm quyền thay đổi cấu trúc.

## Các file source cần ghép

1. `Data/StoreRepository.cs`: tạo phiên bằng dấu bảo mật nhân viên + nhóm quyền.
2. `Data/StoreRepository.Partners.cs`: đối chiếu phiên bằng cùng hai dấu bảo mật.
3. `Services/StaffSessions.cs`: bổ sung thu hồi theo nhân viên **và thế hệ phiên**.
4. `Program.cs`: sau lưu hồ sơ chỉ thu hồi khi dấu bảo mật thực sự đổi; không thu hồi chỉ vì request có trường Username/Password. Thao tác tắt tài khoản cũng gỡ đúng thế hệ trước cập nhật.
5. `Data/SchemaContract.json`: yêu cầu hai cột UUID.
6. `Data/SqlServerDatabaseInitializer.cs`: kiểm tra cột/trigger bằng transaction chỉ đọc; không chạy migration tự động.

Không sửa UI, tồn kho, đơn hủy, CSV/import, giỏ nháp, báo cáo ca, VAT, ưu đãi hay thanh toán. Không thêm `ImportPartnersAsync` hoặc thay đổi hàm này.

## Kiểm thử đã chạy

- Build API .NET 10: 0 lỗi, 0 cảnh báo.
- API .NET 10 thực và PostgreSQL tạm tại cổng 15439 với dữ liệu giả: **105 kiểm tra qua**, gồm 29 kiểm tra phân quyền/phiên. Xem `checks.log`.
- Kiểm tra giữ phiên qua cập nhật hồ sơ SQL và API, cả khi form gửi lại tài khoản/mật khẩu không đổi và khi quản lý sửa chính hồ sơ của mình.
- Kiểm tra phiên cũ bị chặn khi đổi mật khẩu, tên đăng nhập, quyền, tắt/bật nhân viên hoặc nhóm quyền, kể cả phục hồi giá trị cũ giữa hai request.
- Kiểm tra thu hồi đúng thế hệ để token mới không bị gỡ bởi xử lý cập nhật cũ.
- Kiểm tra migration giữ nguyên hồ sơ/thông tin đăng nhập/quyền, chạy lại không đổi phiên bản, giữ 18 bảng và startup từ chối khi thiếu cột hoặc trigger bị tắt.
- Các bài tồn/đơn/CSV của bộ API hiện có cũng được chạy cùng bộ kiểm thử. Không có kiểm thử Supabase thật trong kết quả này.
- Công cụ áp dụng đã được kiểm thử cùng API thực mới build: **107 kiểm tra qua**, gồm tình huống xác minh lỗi phải rollback cả cột/trigger và chạy lại vẫn giữ phiên bản đã có. Xem `installer-checks.log`. Công cụ kiểm tra đúng project Supabase, đủ 18 bảng vật lý (view khuyến mãi được đối chiếu riêng trong contract), giữ hồ sơ/tài khoản/quyền trước khi commit. Build công cụ không lỗi/cảnh báo.

## Trình tự tiếp theo

1. Hai cột và trigger đã được người dùng đồng ý; source tại máy đã ghép. Partner vẫn cần đối chiếu từng phần khi ghép vào nhánh đang sửa riêng.
2. Đóng form/API cũ ở cổng 5001, chạy `Apply-StaffSession.ps1` cạnh báo cáo trong PowerShell. Nhập chuỗi PostgreSQL vào lời nhắc kín; không gửi chuỗi vào chat. Script không lưu kết nối ra file hoặc biến môi trường User.
3. Công cụ chỉ commit khi xác minh thành công. Script đã chạy API mới ở cổng 5001 bằng kết nối trong bộ nhớ tiến trình và tạo báo cáo metadata `live-result.json`, `live-api-status.json`; hai báo cáo đã được đối chiếu, health check trả HTTP 200. Người dùng cần đăng nhập lại một lần lúc triển khai.
4. Kiểm thử bằng hai tài khoản thử nghiệm: sửa hồ sơ vẫn dùng được phiên; đổi bảo mật chặn phiên cũ và giữ phiên vừa đăng nhập.
5. Sau khi chốt phần phiên, xử lý riêng báo cáo ca lưu database và kiểm thử từ máy khác. Phần này chưa được thực hiện trong bản đề xuất.

Chỉ so sánh giá trị hiện tại mà không có dấu thế hệ lưu bền sẽ không bảo toàn yêu cầu phát hiện thao tác tắt/bật hoặc đổi/phục hồi trước request kế tiếp. Cơ chế mới vẫn giới hạn đúng 18 bảng.
