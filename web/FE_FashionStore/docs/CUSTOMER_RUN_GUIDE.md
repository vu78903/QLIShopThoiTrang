# Chạy Task 3 trên Windows / Visual Studio 2022

## Trạng thái bàn giao

Mã nguồn tiếp tục từ ZIP Task 2 đính kèm; không phải project viết lại. Đây là bản triển khai cần build và nghiệm thu trên Windows, chưa phải bản đã được xác nhận chạy hoàn chỉnh. Đọc CUSTOMER_TEST_REPORT.md trước khi thay source đang sử dụng.

## Áp dụng

1. Giữ nguyên thư mục Task 2 đang chạy. Giải nén ZIP Task 3 sang thư mục mới, ví dụ `D:\FashionStore_Task3_Customer`.
2. Mở `FashionStore.sln` trong VS 2022 có workload ASP.NET and web development, .NET Framework 4.8 Developer Pack và .NET 8 SDK. Restore NuGet packages cho solution. Không copy bin/obj/.vs từ bản cũ.
3. Kiểm tra `FashionStore.Backend/appsettings.Development.json`: connection string trỏ tới `LAPTOP-JUQP99HG`, database `FashionStoreDB_Dev`; `Admin:RoleCode` giữ `ADMIN`. Nếu SQL instance khác, chỉ sửa Server. Không trỏ tới FashionStoreDB production.
4. Sao lưu bản DEV trước khi chạy DDL. Trong SSMS chọn **FashionStoreDB_Dev**, kiểm tra schema hiện tại với export tham chiếu rồi chạy `database/06_CustomerFeatures_Dev.sql`. Script bổ sung ba bảng WebWishlist/WebAddress/WebPasswordReset, không sửa bảng cũ hoặc procedure. Không cần chạy lại seed hay reset DB.
5. Xác nhận hai procedure `usp_XacNhanDonOnline`, `usp_HuyDonOnline` từ Task 2 đã tồn tại và đúng bản DEV. Task 3 không thay chúng. Nếu chưa có, đọc tài liệu Task 2 trước khi áp dụng script 04; không chạy lại một cách mặc định.
6. Từ thư mục solution:
   ```powershell
   dotnet build .\FashionStore.Backend\FashionStore.Backend.csproj
   dotnet test .\FashionStore.Backend.Tests\FashionStore.Backend.Tests.csproj
   dotnet run --project .\FashionStore.Backend --launch-profile http
   ```
   Nếu tên launch-profile khác, dùng profile đang có trong Properties/launchSettings.json. Có thể dùng cấu hình rõ ràng:
   ```powershell
   $env:ASPNETCORE_ENVIRONMENT='Development'
   dotnet run --project .\FashionStore.Backend --urls http://127.0.0.1:5000
   ```
7. Mở `http://127.0.0.1:5000/health/ready`; cần `database-connected`. Swagger ở `/swagger` trong Development.
8. Trong VS, đặt `FashionStore.Web` làm Startup Project; kiểm tra `FashionStoreApiBaseUrl` trong Web.config là `http://127.0.0.1:5000`. Build MVC với `MvcBuildViews=true` (Developer PowerShell):
   ```powershell
   msbuild .\FashionStore.Web\FashionStore.Web.csproj /p:Configuration=Debug /p:MvcBuildViews=true
   ```
   Sau đó F5 bằng IIS Express. Khi dùng email reset, bật HTTPS IIS Express.
9. Đăng nhập Customer và Admin tại cùng `/Account/Login`. Admin vẫn đến Dashboard riêng. Không tạo tài khoản admin mới trong bản này.

## Cấu hình cửa hàng / chuyển khoản

Thêm các key dưới `appSettings` trong `FashionStore.Web/Web.config` với thông tin **đã xác nhận của cửa hàng**:

```xml
<add key="StorePhone" value="" />
<add key="StoreEmail" value="" />
<add key="StoreAddress" value="" />
<add key="BankName" value="" />
<add key="BankAccount" value="" />
<add key="BankOwner" value="" />
```

Để trống thì trang thông báo chưa có thông tin; không bịa địa chỉ hoặc tài khoản ngân hàng. Chuyển khoản vẫn chưa thanh toán cho đến khi cửa hàng xác minh theo quy trình hiện có.

## Email reset

Backend đọc cấu hình `Recovery:SmtpHost`, `SmtpPort` (mặc định 587), `SmtpUser`, `SmtpPassword`, `From`, `ResetUrl`. Cấu hình bằng biến môi trường/user secrets, không commit mật khẩu vào source. Ví dụ tên biến PowerShell: `$env:Recovery__SmtpHost`, `$env:Recovery__From`, `$env:Recovery__ResetUrl`. ResetUrl phải là địa chỉ HTTPS thật của trang MVC `/Account/Reset` (port IIS Express đang dùng), không phải URL API. SMTP bật TLS. Chưa có cấu hình: API trả 503 rõ ràng cho mọi email, không giả lập gửi.

Token random 256-bit, chỉ lưu SHA-256, hết hạn 20 phút, dùng một lần. Reset kiểm tra phiên bản hash mật khẩu và vô hiệu các token còn lại của tài khoản trong cùng transaction. Phiên đăng nhập cũ bị cơ chế PasswordVersion hiện có từ chối. Kiểm tra SMTP bằng tài khoản DEV của bạn; không dùng email khách thật để thử.

## Các màn hình

`/`, `/Product`, `/Product/Details/{id}`, `/Cart`, `/Checkout`, `/Orders`, `/Orders/Details/{id}`, `/Customer`, `/Customer/Promotions`, `/Customer/Points`, `/Customer/Addresses`, `/Customer/Wishlist`, `/Customer/Returns`, `/Customer/Return/{orderId}`, `/Customer/Reviews/{productId}`, `/Account/Forgot`, `/Account/Reset`, `/Home/Policy/{shopping|shipping|payment|returns|privacy|terms|faq}`.

Quy trình đổi trả: từ chi tiết đơn hoàn tất → yêu cầu đổi/trả. Cần lịch sử GIAO_HANG/DA_GIAO có thời điểm hợp lệ. Với đổi hàng cần SKU mới đang hoạt động, tồn đủ; số lượng mới bằng số lượng trả. Không giữ tồn ở bước gửi yêu cầu. Form hiện hỗ trợ mô tả, chưa upload minh chứng.

Đánh giá mới vào CHO_DUYET theo schema, chưa công khai ngay. Duyệt đánh giá và xử lý yêu cầu đổi trả thuộc phía quản trị/Windows, chưa thêm giao diện quản trị trong Task 3.

## Chính sách cần lưu ý trước nghiệm thu

- Voucher lấy KhuyenMai toàn hệ thống, không có kho voucher cá nhân. Lượt sử dụng được ghi khi tạo đơn thành công; retry cùng RequestId không ghi thêm. Hủy đơn chưa hoàn lại lượt voucher (procedure gốc không xử lý); nhóm cần chốt quy tắc này trước sử dụng thật.
- Tổng thanh toán phải lớn hơn 0 vì ThanhToan có CHECK SoTien>0. Đơn được giảm hết 100% chưa hỗ trợ; không thay constraint.
- Điểm/hạng đọc KhachHang và LichSuDiem. Không tự cộng điểm trong Checkout. Chưa triển khai quy đổi điểm, ngưỡng hạng hoặc chức năng hoàn tất giao dịch/cộng điểm mới; giữ cơ chế ngoài Web của nhóm.
- Giỏ không giữ tồn, xác nhận Admin mới trừ kho. Hủy Customer dùng procedure cũ với MaNV=NULL, có kiểm tra ownership và khóa đơn trước khi gọi.
- Ảnh editorial ở trang chủ giữ URL ngoài từ source đầu vào, có fallback. Cần kiểm tra ảnh thật trên máy chạy.

## Rollback

Đổi lại source Task 2 trong thư mục riêng nếu cần. Ba bảng bổ sung không tác động luồng Task 2. Chỉ chạy `06_CustomerFeatures_Rollback_Dev.sql` khi bảng chưa có dữ liệu; script từ chối xóa bảng có dữ liệu. Nếu đã dùng, giữ dữ liệu và lập kế hoạch backup/archive riêng. Không tự chạy rollback trên DB nhóm đang sử dụng.

## Ảnh minh chứng cần chụp

1. Home và catalog ở 1366px/1920px, 768px, 375px.
2. Bộ lọc size+màu+giá với kết quả và phân trang.
3. SKU/tồn thay đổi, ảnh sản phẩm và giỏ hàng.
4. Checkout áp dụng mã hợp lệ/không hợp lệ; trang đặt hàng thành công.
5. Customer xem trạng thái trước và sau Admin xác nhận; hủy hợp lệ.
6. Dashboard, hồ sơ, địa chỉ, wishlist, lịch sử điểm thực.
7. Yêu cầu đổi trả hợp lệ và trường hợp quá hạn/khác chủ.
8. Đánh giá chờ duyệt; sau duyệt hiển thị công khai.
9. Reset hết hạn/dùng lại bị từ chối; che token/password/email nhạy cảm khi chụp.
10. Build MVC/API, Test Explorer, Swagger và health/ready thực tế.
