# Task 1 — Admin Authentication + Dashboard (FashionStore v2 / DEV)

## 1. Đã thay đổi

- Backend: ánh xạ `dbo.NhanVien`, `dbo.PhanQuyen`, `dbo.HoaDon` (chỉ đọc) từ schema DEV đã xuất; thêm `POST /api/admin/auth/login`, `GET /api/admin/auth/me`, `POST /api/admin/auth/logout`, `GET /api/admin/dashboard`.
- Session Admin: token riêng, lưu digest SHA-256 trong memory, tự hết hạn sau 60 phút; kiểm tra trạng thái nhân viên, trạng thái quyền, **MaQuyen nằm trong allowlist** và hash mật khẩu trên mỗi request. Token Customer không truy cập API Admin.
- MVC: `/Admin/Login`, `/Admin/Index`, POST `/Admin/Logout` riêng, cookie Admin được bảo vệ bằng `MachineKey.Protect`, không dùng cookie FormsAuth Customer, anti-forgery token cho form POST.
- UI: layout sidebar/desktop/mobile, thống kê sản phẩm, SKU, SKU sắp hết, khách hàng, đơn online và 8 đơn gần đây. Các mục CRUD/duyệt đơn ghi rõ **sắp có** thay vì liên kết giả.
- Tool DEV: tạo mật khẩu mạnh bằng `PasswordHasher<T>` cho nhân viên được cấp quyền. Không chèn password cố định vào SQL hay source.
- Chưa làm: Checkout, Order management, CRUD sản phẩm, đổi trả, khuyến mãi. Đây là Task 2+.

## 2. Kiểm tra bảng trước khi thay đổi dữ liệu

Chỉ mở **database DEV** `FashionStoreDB_Dev` trên SSMS.

1. Chạy file `database/03_Verify_Admin_Dev_ReadOnly.sql` để đọc role và nhân viên hiện có.
2. Nếu đã có nhân viên và MaQuyen quản trị thực sự, dùng mã nhân viên/role đó. Kiểm tra config `FashionStore.Backend/appsettings.Development.json` → `Admin:RoleCode` phải **khớp** `dbo.PhanQuyen.MaQuyen`.
3. Nếu muốn tạo một tài khoản demo DEV mới, **đọc và tự quyết định** chạy `database/02_Seed_WebAdmin_Dev_ReviewFirst.sql`. File này chỉ hoạt động với database có tên `FashionStoreDB_Dev`; tạo role `ADMIN` và nhân viên `WEBADMIN001` (ban đầu mật khẩu NULL), không can thiệp tài khoản khác. Không chạy script này trên database production.

## 3. Đặt mật khẩu admin (DEV only)

Trong Developer PowerShell hoặc terminal Visual Studio, tại thư mục chứa `FashionStore.sln`:

```powershell
$env:FASHIONSTORE_DEV_CONNECTION = 'Server=LAPTOP-JUQP99HG;Database=FashionStoreDB_Dev;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
$env:FASHIONSTORE_ADMIN_ROLE_CODE = 'ADMIN'
dotnet run --project .\tools\SetAdminPassword\SetAdminPassword.csproj -- WEBADMIN001
```

Công cụ yêu cầu nhập mật khẩu **2 lần qua bàn phím**, không echo mật khẩu lên màn hình hay ghi plaintext vào file. Từ chối nếu database không phải `FashionStoreDB_Dev`, nếu nhân viên không active/role không active hoặc terminal không tương tác.

Nếu dùng role/employee khác, đổi cả mã trong lệnh trên và `Admin:RoleCode` trong `appsettings.Development.json`.

> Không lưu password mẫu/connection string chứa mật khẩu vào Git. `Admin:RoleCode` ở cấu hình mặc định là rỗng để từ chối mọi quyền Admin ngoài môi trường DEV. Gán role thật theo thiết kế sản phẩm trong môi trường triển khai.

## 4. Chạy

- Yêu cầu: Visual Studio 2022 có .NET 8 SDK và ASP.NET & web development workload; SQL Server DEV chạy sẵn.
- Mở `FashionStore.sln`, restore NuGet và build lại.
- Chạy `FashionStore.Backend` qua `dotnet run --project .\FashionStore.Backend --urls http://127.0.0.1:5000`.
- Kiểm tra `/health/ready` trả `200 database-connected`, `/swagger` có `/api/admin/auth/*`, `/api/admin/dashboard`.
- Chạy `FashionStore.Web` bằng Visual Studio/IIS Express, mở `http://localhost:<port>/Admin/Login` (hoặc `https://localhost:<port>/Admin/Login` nếu HTTPS).
- Đăng nhập mã nhân viên + mật khẩu vừa tạo. Dashboard phải hiển thị số liệu từ SQL.
- Logout, truy cập lại `/Admin/Index` sẽ chuyển về `/Admin/Login`.
- Đăng nhập Customer riêng và thử truy cập `/Admin/Index` phải yêu cầu login Admin.

## 5. Kiểm thử trên Windows

```powershell
dotnet test .\FashionStore.Backend.Tests\FashionStore.Backend.Tests.csproj --configuration Release
msbuild .\FashionStore.Web\FashionStore.Web.csproj /restore /p:RestorePackagesConfig=true /t:Rebuild /p:Configuration=Debug /p:MvcBuildViews=true /m /verbosity:minimal
```

Kiểm tra `AdminSecurityTests`: unauthorized, role mismatch, disabled employee, session expired/revoked, logout, dashboard, isolation Customer/Admin. Các test SQL cũ 15 bài cần chạy trên máy Windows với `FashionStoreDB_Dev` (xem `docs/ACCOUNT_CART.md`).

**Trạng thái xác minh trong gói này:** đã kiểm tra cấu trúc source, schema/export, các đường dẫn và cấu hình. Máy đóng gói không có `dotnet`, MSBuild/SQL Server/IIS Express nên chưa thể xác nhận build/test chạy thành công. Không coi Task 1 đã nghiệm thu trước khi chạy các bước trên.

## 6. Quy tắc kỹ thuật

- Không mở rộng quyền dựa trên `TenQuyen`, `ChucVu`, hay bất cứ nhân viên nào có mật khẩu; phải khớp `Admin:RoleCode`.
- Session Admin tách biệt, có thể mất khi khởi động lại API. Tài khoản Customer không dùng chung bảng với Admin.
- Không ghi log access token/password; chỉ sử dụng HTTPS khi triển khai thật. Cookie DEV có thể hoạt động trên HTTP localhost, môi trường production yêu cầu HTTPS/machineKey cố định và bảo vệ.
- `Admin Dashboard` hiện chỉ đọc: không tự trừ tồn kho, không tự đổi trạng thái đơn. Task 2 phải dùng giao dịch SQL và nghiệp vụ xác nhận đã review.
