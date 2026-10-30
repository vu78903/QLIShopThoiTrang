# Task 1 (2026-10-18): Admin Authentication + Dashboard

- Admin Bearer API with role allowlist and independent hashed sessions; password storage via Identity PBKDF2.
- Admin MVC login/dashboard/logout, encrypted separate cookie and anti-CSRF forms; Customer unaffected.
- DEV-only admin seed, password utility and read-only verification; dashboard reads SQL Server.
- Admin security tests created; not executed in this Linux environment (SDK/MSBuild/SQL/IIS unavailable).
- Next tasks: Checkout/Orders, Admin orders/products, promotions, returns, customer reviews.

# Danh sách thay đổi

Các mục trước phần "Checkpoint Account/Cart" mô tả lịch sử bàn giao Product API. Phạm vi hiện tại bổ sung Account/Cart và thay đổi hai Razor view cùng Web.config như liệt kê ở cuối.

Nguồn chính: đúng `web(1).zip` được cung cấp. Bản làm việc có thêm backend; file ZIP đầu vào được giữ nguyên.

## File source gốc đã sửa

- `FashionStore.Web/FashionStore.Web.csproj` — bật lại kiểm tra thiếu gói NuGet; xem docs/FIX_ROSLYN_CSC.md.

- `FashionStore.Web/Controllers/HomeController.cs`
- `FashionStore.Web/Controllers/ProductController.cs`
- `FashionStore.Web/Models/CatalogModels.cs`
- `FashionStore.Web/Services/CatalogRepository.cs`
- `FashionStore.sln`

`CatalogRepository` chuyển sang async Product API; Controller await, bỏ gọi categories trùng và trả trạng thái lỗi đúng; Model thêm MaLoai và dùng long cho tổng tồn; solution thêm hai project.

## File thêm

- `.gitignore`
- `FashionStore.Backend.Tests/ApiFactory.cs`
- `FashionStore.Backend.Tests/FashionStore.Backend.Tests.csproj`
- `FashionStore.Backend.Tests/FoundationTests.cs`
- `FashionStore.Backend.Tests/MvcClientContractTests.cs`
- `FashionStore.Backend.Tests/SqlCatalogFixture.cs`
- `FashionStore.Backend.Tests/SqlCatalogIntegrationTests.cs`
- `FashionStore.Backend.Tests/SqlTranslationTests.cs`
- `FashionStore.Backend.Tests/TestResults/ProductApi.trx`
- `FashionStore.Backend/Controllers/CategoriesController.cs`
- `FashionStore.Backend/Controllers/ProductsController.cs`
- `FashionStore.Backend/DTOs/CatalogDtos.cs`
- `FashionStore.Backend/Data/FashionStoreDbContext.cs`
- `FashionStore.Backend/FashionStore.Backend.csproj`
- `FashionStore.Backend/Middleware/ApiExceptionHandler.cs`
- `FashionStore.Backend/Models/CatalogEntities.cs`
- `FashionStore.Backend/Program.cs`
- `FashionStore.Backend/Properties/AssemblyInfo.cs`
- `FashionStore.Backend/Properties/launchSettings.json`
- `FashionStore.Backend/Services/CatalogService.cs`
- `FashionStore.Backend/appsettings.Development.example.json`
- `FashionStore.Backend/appsettings.json`
- `README_BAT_DAU.md`
- `database/01_SeedCatalog_Dev_ReviewFirst.sql`
- `database/Export-Schema-ReadOnly.sql`
- `database/Export-Schema.ps1`
- `database/Verify-CatalogSchema-ReadOnly.sql`
- `docs/API.md`
- `docs/MAPPING_VA_GIOI_HAN.md`
- `docs/openapi.v1.json`
- `scripts/Test-SqlCatalog.ps1`
- `verification/FashionStore.MvcCompileCheck.csproj`

- `evidence/`: log build/test, TRX, OpenAPI/runtime smoke và báo cáo bảo toàn UI.

- `docs/KET_QUA_KIEM_THU.md`

## Bảo toàn

- Không xóa file source gốc. 71 file View/Content/Scripts được so byte và giữ nguyên.
- Không thay đổi Web.config, routing, header/footer hoặc thư viện giao diện.
- Không ghi đè backup hoặc source ZIP. Không tạo/migrate database.
- Gói source bỏ thư mục sinh tự động bin/obj/.vs/packages và file *.user; NuGet restore được hướng dẫn trong README.

Các tệp tài liệu/evidence cuối cùng cũng được liệt kê trong ZIP; không bao gồm SDK/SQL engine tải về, dump hoặc credential của môi trường kiểm tra.


## Cập nhật lỗi Roslyn sau khi chạy Windows

- Bổ sung `docs/FIX_ROSLYN_CSC.md` và hướng dẫn Reinstall → Rebuild trong README.
- `evidence/roslyn-build-guard.json`: guard phát hiện gói thiếu và cho qua sau khi restore. Chưa nghiệm thu lại IIS Express trên Windows.

## Cập nhật script PowerShell từ lỗi thực tế trên Windows

- `scripts/Test-SqlCatalog.ps1`, `database/Export-Schema.ps1`: thay các setter dạng `.DataSource`, `.InitialCatalog` và các thuộc tính builder khác bằng indexer với tên khóa SQL chuẩn; lấy chuỗi qua `get_ConnectionString()` để tránh dictionary adapter của PowerShell.
- Thêm `docs/FIX_POWERSHELL_SQL.md` và hướng dẫn xử lý execution policy trong README.
- Đã tái hiện lỗi gốc và chạy 20 kiểm tra regression của script trên PowerShell 7.4.6/Linux; chi tiết `evidence/powershell-sql-builder-fix.json`. Không mở kết nối SQL trong kiểm tra này; lần chạy 31 test trên Windows vẫn chờ kết quả.
- Người dùng đã gửi bằng chứng schema catalog PASS, API đọc dữ liệu mẫu từ DEV, MVC List/Details hiển thị ảnh và tồn theo SKU. Đây là kiểm tra thủ công, tách biệt với kết quả test tự động ban đầu.

## Sửa mặc định OutputDirectory của exporter

- `database/Export-Schema.ps1`: tính thư mục mặc định sau parameter binding, xử lý lỗi Join-Path/PSScriptRoot rỗng được báo trên Windows.
- Bổ sung lệnh truyền OutputDirectory tường minh trong README và tài liệu sửa PowerShell.
- `evidence/powershell-export-parameters.json`: bốn kiểm tra parse/bind/thư mục mặc định và thư mục tường minh PASS trên PowerShell 7.4.6, không truy vấn SQL.
- Ghi nhận người dùng đã chạy 31/31 test ứng dụng PASS; chưa nhận TRX chi tiết. Không thay đổi C# API/MVC trong bản sửa exporter.

## Checkpoint Account/Cart — 16/10/2026

- API: Models/CustomerEntities.cs; DTOs/CustomerDtos.cs; Services/AccountService.cs, CartService.cs, CustomerSessions.cs, CustomerPasswordVerifier.cs; Middleware/CustomerAuthentication.cs, CustomerSecurityOperationFilter.cs; Controllers/AccountController.cs, CartController.cs. Sửa DbContext, Program và exception handler để map schema thật, DI, auth, rate limit, conflict và errors.
- MVC: thêm Models/CustomerModels.cs, Services/CustomerApiClient.cs, Helpers/CustomerAuthentication.cs, AccountController/CartController, sáu view Account/Cart và Content/customer.css. Sửa Web.config cho Forms Auth, csproj để include file, header hai icon và Product/Details form thêm giỏ. Không đổi routing, CSS/JS catalog, Home hoặc các view còn lại.
- Test: thêm CustomerSecurityTests, CustomerSqlIntegrationTests, CustomerMvcClientTests, CustomerMappingTests, UnconfiguredPublicEndpointsTests; thêm EF InMemory chỉ trong test và link đúng mã MVC client. Sửa compile check net48 thêm reference cần cho DataAnnotations/dynamic.
- Giữ snapshot schema và TRX Windows bản trước, thêm evidence test/build/runtime bản mới, cập nhật OpenAPI và tài liệu Account/Cart.
- Script Test-SqlCatalog.ps1 dùng lại phần builder đã sửa, cập nhật thông báo fixture để bao gồm customer/account/cart. Không tạo/migrate database, không sửa/cài procedure hoặc dùng dữ liệu mock trong API runtime.

## Task 2 (2026-10-19) — Checkout + Online Orders (DEV pilot)

- REST customer order API with serializable checkout, server-side price/stock checks, order/payment/status snapshots and form-level idempotency.
- Customer Checkout and Orders MVC pages; Checkout button from Cart; order history link in footer.
- Admin order list/detail, confirm/cancel buttons; stock movement delegated to the existing SQL procedures.
- DEV-only reviewed procedure script and read-only SQL verification script.
- MVC `.csproj` explicit includes for new controllers/models/views; package restore compatibility retained.
- Important: No live DB or Windows build was run in the artifact environment. SQL procedure installation is intentionally manual and restricted to a disposable `FashionStoreDB_Dev` database.
