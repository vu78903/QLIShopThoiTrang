# Account & Cart — Checkpoint 4

Triển khai từ bản Product API đã có TRX Windows 31/31 PASS. Mapping bổ sung dựa trực tiếp vào metadata người dùng xuất từ SQL Server 2022: `database/schema-reference/FashionStoreDB_Dev_schema_20261015_170000_433_bf029ba2.json`. Không tạo/migrate database hoặc cài lại procedure.

## Mapping và phạm vi

| Bảng thực tế | Cách dùng |
|---|---|
| KhachHang | Hồ sơ, một địa chỉ mặc định DiaChi; email/SĐT unique, RowVer cho kiểm soát cập nhật đồng thời. Điểm/hạng chỉ đọc. |
| TaiKhoanKhachHang | MaKH là PK/FK; username unique, MatKhauHash, trạng thái và lần đăng nhập cuối. |
| GioHang | Identity bigint; một giỏ cho mỗi MaKH theo unique constraint hiện có. |
| ChiTietGioHang | PK (MaGioHang, MaSKU), số lượng nguyên dương, FK tới SKU; xóa giỏ cascade chi tiết theo schema. |

Chưa có bảng sổ địa chỉ riêng; hiện chỉ chỉnh địa chỉ mặc định trong hồ sơ. Chưa thêm chức năng nhiều địa chỉ, guest cart, quên mật khẩu/email verification, admin/staff API hoặc liên kết tài khoản vào khách hàng POS có sẵn. Đăng ký với SĐT/email đã tồn tại trả 409, không tự nhận quyền sở hữu khách hàng cũ. Khôi phục tài khoản/đồng bộ hash định dạng khác cần thống nhất với nhóm WinForms.

## API

| Method | Path | Quyền và hành vi |
|---|---|---|
| POST | /api/account/register | Công khai; tạo customer/account/cart trong một SaveChanges transaction. Trả 201, không tự đăng nhập. |
| POST | /api/account/login | Công khai; trả accessToken, tokenType=Bearer, expiresAtUtc. |
| GET | /api/account/me | Customer; chỉ hồ sơ của người đăng nhập. |
| PUT | /api/account/me | Customer; cập nhật họ tên, SĐT, email, địa chỉ, nhận khuyến mãi với rowVersion từ GET. |
| POST | /api/account/password | Customer; kiểm tra mật khẩu cũ, đổi hash và làm vô hiệu tất cả phiên cũ. |
| POST | /api/account/logout | Customer; hủy phiên hiện tại tại server. |
| GET | /api/cart | Customer; đọc giỏ của chính mình, giá/tồn hiện tại từ SQL. |
| POST | /api/cart/items | Customer; cộng quantity vào SKU hiện có hoặc thêm dòng mới. |
| PUT | /api/cart/items/{sku} | Customer; đặt quantity tuyệt đối; gọi lặp cùng giá trị không tăng thêm. |
| DELETE | /api/cart/items/{sku} | Customer; xóa dòng, gọi lại vẫn hợp lệ. |
| DELETE | /api/cart | Customer; xóa toàn bộ chi tiết, giữ giỏ. |

Các API cá nhân chỉ lấy MaKH từ phiên đăng nhập; không nhận MaKH/quyền/giá/điểm từ request. DTO từ chối trường JSON ngoài contract. Tồn quản lý theo SKU; giỏ không giữ hàng hoặc trừ kho. Tạm tính là tổng giá SKU hiện tại nhân số lượng, chưa áp dụng giảm giá, điểm hoặc vận chuyển. Dòng ngừng bán/vượt tồn được giữ để người dùng xóa, kèm issue và canCheckout=false. canCheckout chỉ biểu thị giỏ hợp lệ tại thời điểm đọc, không tạo hay xác nhận đơn.

Cart mutation dùng Serializable và UPDLOCK/HOLDLOCK trên PK khách hàng để tuần tự hóa thao tác cùng chủ giỏ, kể cả trường hợp khách cũ chưa có GioHang. Tra cứu SKU/dòng giỏ dùng collation SQL, lưu mã SKU chuẩn từ bảng biến thể. Thêm giỏ bằng POST có tính cộng dồn: sau lỗi mạng, tải lại giỏ trước khi gửi lại để tránh cộng hai lần. PUT/DELETE có thể gọi lặp.

## Request mẫu (chỉ minh họa contract)

Đăng ký:

```json
{
  "userName":"demo_customer",
  "password":"Use a unique password here",
  "fullName":"Khách hàng kiểm thử",
  "phone":"0901234567",
  "email":null,
  "address":"Địa chỉ kiểm thử DEV"
}
```

Không dùng thông tin mẫu để ghi đè bản ghi khách hàng đã có. Username mới: 3–100 ký tự ASCII gồm chữ/số/dấu chấm/gạch ngang/gạch dưới; password: 12–128 ký tự; SĐT: 9–15 chữ số, có thể bắt đầu bằng +. Họ tên tối đa 150, địa chỉ 300, email ASCII tối đa 254 theo cột varchar. Profile GET có rowVersion base64; PUT phải gửi nguyên giá trị đọc được, không sửa điểm/hạng.

Thêm giỏ:

```json
{"sku":"WEB-DEMO-001-M","quantity":1}
```

Đặt lại số lượng:

```json
{"quantity":2}
```

Status: 400 dữ liệu sai/trường thừa; 401 chưa đăng nhập, sai mật khẩu hoặc phiên hết hạn; 404 SKU không kinh doanh; 409 trùng thông tin, hồ sơ cũ, vượt tồn hoặc xung đột dữ liệu; 429 vượt giới hạn thử tài khoản; 503 SQL/config không khả dụng. Response nhạy cảm không cache.

## Phiên và mật khẩu

- Hash dùng ASP.NET Core PasswordHasher (Identity V3, PBKDF2, 210.000 iterations). Hash Identity cũ hợp lệ được nâng khi đăng nhập nếu cần; không có fallback plaintext/MD5 hoặc tự nhận định dạng WinForms chưa biết.
- API phát token ngẫu nhiên 256 bit, chỉ giữ digest SHA-256 trong IMemoryCache cùng MaKH, phiên bản hash và hạn tuyệt đối 60 phút. Mỗi request được bảo vệ kiểm tra trạng thái account/customer và phiên bản mật khẩu trong SQL. Logout hủy một phiên; đổi mật khẩu vô hiệu tất cả phiên cũ.
- Đây là cấu hình một backend DEV: restart backend sẽ đăng xuất các phiên; chưa dùng distributed/persistent session store. Không có JWT signing secret cần nhập. Chọn store dùng chung là bước triển khai nhiều instance sau này.
- MVC lưu token bên trong FormsAuthenticationTicket đã mã hóa/xác thực bởi MachineKey, cookie HttpOnly/SameSite=Lax và Secure khi request HTTPS. Không dùng localStorage, không gắn bearer vào SharedHttp.DefaultRequestHeaders. Tất cả POST MVC có anti-forgery. ReturnUrl chỉ chấp nhận URL local.
- Local HTTP dành cho máy phát triển; triển khai thật phải dùng HTTPS cho cả Website và API, cùng cấu hình proxy/key/session phù hợp.
- Login/register/password giới hạn 20 request/5 phút/IP. Với MVC gọi server-to-server, các khách có thể dùng chung quota IP của máy MVC. Logout mất kết nối vẫn xóa cookie local; phiên API chưa nhận được logout sẽ tự hết hạn.

Tham khảo: [ASP.NET Identity configuration](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-8.0), [MVC anti-forgery](https://learn.microsoft.com/en-us/aspnet/mvc/overview/security/xsrfcsrf-prevention-in-aspnet-mvc-and-web-pages), [OWASP session management](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html).

## Chạy trên Windows

1. Dừng backend và F5; giải nén source vào thư mục mới. Chép file appsettings.Development.json đang kết nối được từ bản trước sang FashionStore.Backend của bản mới. Giữ Web.config của bản mới vì có cấu hình Forms Authentication.
2. Mở FashionStore.sln, Restore NuGet Packages, Rebuild Solution. Chạy backend bằng lệnh cũ tại thư mục solution. Chọn FashionStore.Web làm startup project và F5.
3. Bấm biểu tượng tài khoản → Đăng ký → Đăng nhập → lưu hồ sơ/địa chỉ. Tạo account mới với SĐT/email chưa được dùng trong DEV. Kiểm tra hai tab sửa hồ sơ cũ trả lỗi yêu cầu tải lại.
4. Mở sản phẩm, chọn SKU còn hàng và số lượng → Thêm vào giỏ. Kiểm tra cộng SKU trùng, sửa lượng, xóa, xóa hết, hàng hết tồn, hai tài khoản có giỏ độc lập.
5. Đổi mật khẩu phải yêu cầu đăng nhập lại; mật khẩu cũ không đăng nhập được. Đăng xuất xong không đọc được hồ sơ/giỏ bằng phiên cũ. Tắt backend rồi mở lại sẽ cần đăng nhập lại.
6. Chạy bộ test trên DEV bằng script dưới đây. Script sẽ thêm và dọn dữ liệu catalog TAPI..., tài khoản test tacct..., customer và giỏ liên quan. Không gọi procedure đơn/kho/điểm. Nếu bị tắt cưỡng bức, có thể còn fixture cần kiểm tra; tránh tạo giao dịch thủ công với fixture trong lúc chạy test.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-SqlCatalog.ps1 -ServerInstance 'LAPTOP-JUQP99HG' -TrustServerCertificate
```

Mục tiêu bản này: **62 passed, 0 failed, 0 skipped**. TRX ghi tại FashionStore.Backend.Tests/TestResults/SqlCatalog.trx. Nếu dùng instance khác, thay ServerInstance.

Swagger: POST register/login trước; sao chép accessToken vào Authorize, sau đó thử /me và /cart. Public catalog/login không cần bearer.

## Kết quả và giới hạn

- TRX người dùng đã nhận và đọc: Product API bản trước 31/31 PASS, 0 skipped. Giữ nguyên tại evidence/ProductApi_Windows_31_Passed.trx.
- Bản Account/Cart: Release API build và C# MVC net48 compile PASS, 0 warnings/errors.
- **47 test local PASS, 15 SQL test SKIP**: 24 test cũ không SQL và 23 test mới (12 HTTP/security dùng EF InMemory cho kiểm tra auth, 8 MVC client contract, 2 mapping/SQL translation, 1 public endpoint không config). InMemory không xác minh constraint hoặc transaction của SQL Server.
- 15 test SQL gồm 7 catalog cũ và 8 Account/Cart mới. Bảy catalog cũ đã có bằng chứng Windows ở checkpoint trước; cả 15 phải chạy lại trên bản source này để kiểm tra regression. Tám test mới bao quát atomic signup/duplicate và race, rowversion/overposting, chủ giỏ/PUT/DELETE lặp/case SKU, tồn/ngừng bán, concurrent adds, cập nhật giá/tồn và đổi mật khẩu.
- HTTP Kestrel thật: live/Swagger 200, private endpoint 401, ready/catalog 503 khi cố ý bỏ cấu hình DB. Swagger 14 path phân biệt quyền đúng. Không tuyên bố đăng ký/ghi giỏ SQL thành công tại môi trường này.
- Razor/IIS Express cho các view mới và UAT Windows mới chưa chạy ở đây. Để compile Razor trên Windows, dùng Developer PowerShell for VS2022:

```powershell
msbuild .\FashionStore.Web\FashionStore.Web.csproj /restore /p:RestorePackagesConfig=true /t:Rebuild /p:Configuration=Debug /p:MvcBuildViews=true /m /verbosity:minimal
```

Checkout, Order, Loyalty, Return và Wishlist/Review chưa triển khai. Procedure hiện có vẫn khóa DB_NAME()=FashionStoreDB; cần giải quyết phương án test DEV trước checkpoint giao dịch, không đổi tên database để lách guard.
