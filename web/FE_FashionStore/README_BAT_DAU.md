# MỚI: Task 1 — Đăng nhập Admin + Dashboard (18/10/2026)

Đã bổ sung mã nguồn và hướng dẫn chạy. **ĐỌC TRƯỚC:** [`docs/TASK1_ADMIN_SETUP.md`](docs/TASK1_ADMIN_SETUP.md). Bản gốc Product + Customer + Cart được giữ nguyên. Mở `FashionStore.sln` sau khi giải nén; không cần chuyển nền tảng, không ghi đè SQL Server production. **Chưa xác minh build/test Task 1 trên Windows**, cần chạy các lệnh trong tài liệu này trước khi xem như hoàn tất.

---

# FashionStore — Product API, Account và Cart + MVC

Bản làm việc tiếp tục từ `web(1).zip`, giữ ASP.NET MVC 5 / .NET Framework 4.8 và giao diện hiện có. Backend độc lập dùng ASP.NET Core 8, EF Core SQL Server 8.0.32 và Swagger. Baseline triển khai là **FashionStoreDB v2**, không chuyển sang YodyStore.

**Checkpoint hiện tại: Account/Customer + Cart.** Product API bản trước đã có TRX Windows xác nhận **31/31 PASS**. Bản mới build API Release và C# MVC net48 PASS; **47 test local PASS, 15 SQL test chờ Windows** (gồm 7 catalog regression và 8 Account/Cart mới). Có schema thực tế người dùng xuất và kiểm tra mapping offline. Chi tiết chạy, phạm vi xác thực và giới hạn: `docs/ACCOUNT_CART.md`.

**Cập nhật từ bản đang chạy:** giải nén ra thư mục mới và chép `FashionStore.Backend/appsettings.Development.json` đang dùng sang bản mới; dùng Web.config mới có Forms Authentication. Các form tài khoản/giỏ và nút thêm giỏ đã nối với API. Phiên API hiện lưu trong một backend, hết hạn sau 60 phút; restart backend yêu cầu đăng nhập lại.

## 1. Chuẩn bị database test

Restore `FashionStoreDB_v2_Final_20261013.bak` thành **FashionStoreDB_Dev** trên SQL Server 2022 local/test. Chọn MDF/LDF mới, không dùng WITH REPLACE, không trỏ tới DB chính. Nếu tên `_Dev` đã chứa công việc cần giữ, dùng instance test khác; không ghi đè nó.

Không chạy lại migration/installer ProductionCandidate để thử API; các file đó có `USE [FashionStoreDB]` và không phải schema tạo bản test. Catalog API chỉ SELECT; Account/Cart ghi đúng các bảng khách hàng/tài khoản/giỏ trên DEV. Không gọi sáu procedure kho/đổi trả/điểm, không tạo database hoặc chạy migration.

Trong SSMS chọn `FashionStoreDB_Dev`, chạy `database/Verify-CatalogSchema-ReadOnly.sql`. Nếu có khác biệt, gửi Results/Messages trước khi tiếp tục. PASS của file này chỉ xác nhận **24 cột và bốn primary key dùng bởi catalog**, không thay thế audit toàn bộ 29 bảng/2 view/47 FK/6 SP.

Có thể dùng `database/Export-Schema.ps1` để gửi JSON schema của FashionStoreDB_Dev và YodyStore_Dev, hoàn tất đối chiếu còn thiếu từ Checkpoint 1. Script export/schema-check chưa được chạy trên SQL Server tại đây.

## 2. Cấu hình và chạy backend

Visual Studio 2022 17.8+ với workload ASP.NET, .NET 8 SDK và .NET Framework 4.8 Developer Pack. .NET 8 được chọn để giữ tương thích VS2022 theo yêu cầu. **.NET 8 hết hỗ trợ 10/11/2026**; trước khi sử dụng dài hạn cần chốt nâng backend lên .NET 10 LTS/toolchain phù hợp. Website MVC 5 không phải chuyển nền tảng.

Mở PowerShell tại thư mục chứa `FashionStore.sln`. Tạo cấu hình riêng nếu chưa có:

```powershell
if (-not (Test-Path '.\FashionStore.Backend\appsettings.Development.json')) {
    Copy-Item '.\FashionStore.Backend\appsettings.Development.example.json' '.\FashionStore.Backend\appsettings.Development.json'
}
```

Sửa `Server` theo instance SSMS của bạn. Ví dụ trong JSON:

```json
{
  "ConnectionStrings": {
    "FashionStore": "Server=.\\SQLEXPRESS;Database=FashionStoreDB_Dev;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5"
  }
}
```

`TrustServerCertificate=True` dành cho local DEV có chứng chỉ tự ký. File Development riêng đã được `.gitignore` loại trừ. Không đặt mật khẩu SQL vào file commit. Nếu dùng SQL Authentication, cấu hình qua User Secrets hoặc biến môi trường `ConnectionStrings__FashionStore` trong môi trường riêng; không đưa secret vào source/ZIP/log.

```powershell
dotnet restore .\FashionStore.Backend\FashionStore.Backend.csproj
dotnet build .\FashionStore.Backend\FashionStore.Backend.csproj --configuration Release
dotnet run --project .\FashionStore.Backend\FashionStore.Backend.csproj --launch-profile FashionStore.Backend
```

Mở [Swagger](http://127.0.0.1:5000/swagger). `/health/live` xác nhận server đang chạy. `/health/ready` chỉ kiểm tra kết nối SQL, không chứng minh schema/nghiệp vụ đã audit. Chưa cấu hình hoặc không kết nối DB sẽ nhận 503 ở endpoint cần DB; Swagger/liveness vẫn mở được.

Development chỉ chấp nhận connection string tới `FashionStoreDB_Dev`. Không có CORS vì MVC hiện gọi API từ server. Không có endpoint `/api/app-state` trong backend mới.

## 3. Chạy Website MVC 5

Khi backend đang chạy bằng cửa sổ PowerShell trên:

1. Mở `FashionStore.sln` trong VS2022; chọn **FashionStore.Web** làm Startup Project.
2. Restore NuGet Packages cho solution. Gói ZIP source không mang `packages`, `bin`, `obj`, `.vs`. Package CodeDom 2.0.1 có trong packages.config cần được restore.
3. `FashionStore.Web/Web.config` đã có `FashionStoreApiBaseUrl=http://127.0.0.1:5000`; sửa URL này nếu backend chạy cổng khác.
4. Chạy F5. Kiểm tra `/`, `/Product`, `/Product/Details/{MaSP}` bằng mã thật trong DEV.

Để clean build cả C# và Razor, mở Developer PowerShell for VS2022 tại thư mục solution:

```powershell
msbuild .\FashionStore.Web\FashionStore.Web.csproj /restore /p:RestorePackagesConfig=true /t:Rebuild /p:Configuration=Debug /p:MvcBuildViews=true /m /verbosity:minimal
```

Nếu trang báo thiếu `FashionStore.Web\bin\roslyn\csc.exe`, dừng F5, mở **Tools → NuGet Package Manager → Package Manager Console** trong Visual Studio rồi chạy:

```powershell
Update-Package Microsoft.CodeDom.Providers.DotNetCompilerPlatform -Reinstall -ProjectName FashionStore.Web
```

Sau đó **Build → Rebuild Solution**, kiểm tra file `FashionStore.Web\bin\roslyn\csc.exe` đã xuất hiện và chạy F5 lại. Lệnh này dùng Package Manager Console của Visual Studio. Giữ gói CodeDom 2.0.1 đang có; không xóa cấu hình compiler trong Web.config. Bản cập nhật đã bật lại target chuẩn `EnsureNuGetPackageBuildImports` để báo sớm khi chưa restore package, thay vì bỏ qua kiểm tra này.

Đây là bước **chưa chạy ở môi trường Linux**. Lệnh kiểm tra thay thế đã chạy ở đây là `dotnet build verification/FashionStore.MvcCompileCheck.csproj`; nó xác minh C# theo net48, không compile Razor hoặc host System.Web/IIS.

Website gọi async `/api/products` và `/api/categories`; list chỉ tải đúng trang. SKU/size/màu/giá/tồn/ảnh lấy từ response backend. Tài khoản và giỏ hàng đã được nối; yêu thích và thanh toán chờ checkpoint sau. Chỉ các view header/chi tiết liên quan và view Account/Cart mới được cập nhật, không thiết kế lại catalog.

## 4. Chạy kiểm thử

Chạy phần không cần SQL:

```powershell
dotnet test .\FashionStore.Backend.Tests\FashionStore.Backend.Tests.csproj --configuration Release --logger 'trx;LogFileName=ProductApi.trx'
```

Nếu chưa có `FASHIONSTORE_TEST_CONNECTION`, 15 test tích hợp sẽ SKIP và ghi rõ BLOCKED. Không coi lần chạy đó là nghiệm thu SQL.

Sau khi restore DEV và kiểm tra schema, dùng Windows Authentication:

```powershell
.\scripts\Test-SqlCatalog.ps1 -ServerInstance '.\SQLEXPRESS' -TrustServerCertificate
```

Hoặc SQL Authentication qua hộp thoại:

```powershell
$testCredential = Get-Credential
.\scripts\Test-SqlCatalog.ps1 -ServerInstance 'localhost' -Credential $testCredential -TrustServerCertificate
Remove-Variable testCredential
```

Runner này chạy toàn bộ 62 test; 15 test SQL **tạm thêm dữ liệu catalog TAPI..., tài khoản tacct..., customer và giỏ liên quan vào FashionStoreDB_Dev rồi xóa khi kết thúc**. Không tạo DB, không gọi giao dịch đơn/kho/điểm, không thay dữ liệu nghiệp vụ cũ. Nếu kết thúc process cưỡng bức giữa chừng, có thể còn dữ liệu TAPI... cần kiểm tra thủ công trên DEV. API runtime không có fixture hoặc fallback dữ liệu minh họa.

Nếu PowerShell chặn script chưa ký số, gọi phiên riêng cho lần chạy này:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-SqlCatalog.ps1 -ServerInstance '.\SQLEXPRESS' -TrustServerCertificate
```

Thay `ServerInstance` bằng đúng server/instance đã kết nối thành công trong SSMS và backend. Nếu gặp `Keyword not supported: 'DataSource'`, cập nhật `scripts/Test-SqlCatalog.ps1` từ bản source đã sửa. Bản sửa dùng các khóa connection string chuẩn và sửa cả `database/Export-Schema.ps1`; xem `docs/FIX_POWERSHELL_SQL.md`. Không cần ghi đè cấu hình Development đang dùng của bạn.

Test SQL kiểm tra: các bộ lọc, sort/phân trang, sản phẩm mới, hàng hết tồn, SKU ngừng bán, category ngừng bán, ảnh, 404 và mapping MVC → API → SQL. Gửi lại file TRX cùng schema JSON/Results để xác nhận dữ liệu thực tế và mở checkpoint tiếp theo.

## 5. Database DEV trống

Catalog trống là kết quả hợp lệ. Không có sản phẩm mock trong API. Có thể review `database/01_SeedCatalog_Dev_ReviewFirst.sql` được giữ nguyên từ ZIP web: mặc định `@ApplyDemo=0`, chỉ thêm mẫu khi bạn chủ động bật trên DEV. Dữ liệu/ảnh của script này là minh họa, không phải dữ liệu bán hàng thật.

Các integration test tự dọn fixture nên sau test catalog có thể lại trống. Ảnh chỉ hiển thị nếu URL/đường dẫn lưu trong DB truy cập được từ Website; upload/lưu trữ media chưa thuộc checkpoint này.

## 6. Phạm vi bàn giao

- Bốn endpoint catalog, validation, DTO, DI, async, Swagger, lỗi ProblemDetails và health checks.
- Mapping EF theo DDL gốc `01_Create_FashionStoreDB.sql` và cột `SanPham.DoiTuong` của migration v2, có ghi nguồn trong `docs/MAPPING_VA_GIOI_HAN.md`.
- MVC dùng Product API và Customer/Cart API; giữ routing và giao diện catalog, bổ sung form tài khoản/giỏ và nối các nút liên quan.
- Đã triển khai Account/Customer và Cart theo schema hiện có; chưa triển khai Checkout, Order, Loyalty hoặc Return. Xem docs/ACCOUNT_CART.md để chạy 62 test và UAT các view mới.

File chính: `docs/API.md`, `docs/KET_QUA_KIEM_THU.md`, `docs/MAPPING_VA_GIOI_HAN.md`, `CHANGELOG.md`, và `evidence/`.

Tham khảo Microsoft:

- [SDK/Visual Studio compatibility](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs)
- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [EF Core SQL Server provider](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/)

## 7. Xuất schema trước Account/Cart

Dùng script đã sửa PowerShell SQL builder và mặc định OutputDirectory. Nếu bản đang dùng báo Join-Path với Path rỗng ở phần param, có thể truyền OutputDirectory tường minh mà chưa cần tải lại source:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\database\Export-Schema.ps1 -ServerInstance '.\SQLEXPRESS' -Database 'FashionStoreDB_Dev' -OutputDirectory '.\database\exports' -TrustServerCertificate
```

Thay ServerInstance bằng đúng server/instance SSMS đã dùng. Script xuất metadata, không đọc dữ liệu khách hàng. Bản source mới khởi tạo thư mục mặc định trong thân script sau khi bind tham số, thay vì gọi Join-Path trong param.
