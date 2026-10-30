# Sửa lỗi Keyword not supported: 'DataSource'

Lỗi phát sinh trong script PowerShell khi tạo connection string, trước khi `dotnet test` được gọi. Backend và cấu hình SQL đã kết nối được của người dùng không phải phần cần sửa cho lỗi này.

PowerShell xử lý SqlConnectionStringBuilder như IDictionary. Các setter `.DataSource`, `.InitialCatalog`, `.IntegratedSecurity`, `.ConnectTimeout`, `.ApplicationName`, `.UserID` không phải tên khóa SQL hợp lệ trong đường xử lý đó. Hai script đã được sửa để dùng indexer với các khóa `Data Source`, `Initial Catalog`, `Integrated Security`, `Connect Timeout`, `Application Name`, `User ID`, cùng getter `get_ConnectionString()`.

## Cập nhật bản đang dùng trên Windows

Từ ZIP source mới, chép riêng hai file vào đúng thư mục của bản làm việc:

- `scripts/Test-SqlCatalog.ps1`
- `database/Export-Schema.ps1`

Giữ cấu hình Development cá nhân đã kết nối thành công. Mở PowerShell tại thư mục chứa FashionStore.sln, chạy lại lệnh kiểm thử với đúng server đã dùng trong SSMS:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-SqlCatalog.ps1 -ServerInstance '.\SQLEXPRESS' -TrustServerCertificate
```

`ServerInstance` ở đây là ví dụ, phải thay bằng tên server/instance thực tế. Script vẫn cố định database FashionStoreDB_Dev. Kết quả mục tiêu là 31 passed, 0 failed, 0 skipped; chưa có kết quả chạy này thì không ghi PASS cho bảy SQL test.

## Kiểm chứng bản sửa

- Đã tải PowerShell 7.4.6 Linux từ release chính thức và xác minh SHA-256 theo hashes.sha256 cùng release.
- Tái hiện lỗi setter DataSource của mã cũ.
- Parse hai script không có lỗi; gọi script runner thật với `dotnet` được thay bằng hàm kiểm tra để xác minh tham số, connection string Windows/SQL Authentication, certificate switch, mật khẩu tổng hợp có ký tự đặc biệt, bảo toàn biến môi trường khi thành công và thất bại.
- Kiểm tra initializer của exporter với hai database DEV được cho phép và hai chế độ đăng nhập.
- Tổng cộng 20 kiểm tra PASS; bằng chứng ở evidence/powershell-sql-builder-fix.json. Các kiểm tra này không truy vấn SQL và không thay thế bộ 31 test ứng dụng.
- Windows PowerShell 5.1 và SQL Server của người dùng vẫn cần chạy lại sau khi cập nhật.

## Lỗi OutputDirectory / Join-Path khi khởi tạo tham số

Windows báo PSScriptRoot rỗng khi tính mặc định OutputDirectory trong param. Bản sửa bỏ biểu thức mặc định đó và tính Join-Path trong thân script sau parameter binding. Có thể chạy ngay bản cũ bằng cách truyền `-OutputDirectory '.\database\exports'` khi đang ở thư mục solution.

Bốn kiểm tra trên PowerShell 7.4.6 PASS: parse script, mặc định param không còn tham chiếu script root, gọi script thật không truyền OutputDirectory lấy đúng thư mục exports cạnh script, và truyền đường dẫn tương đối giữ đúng giá trị đã chỉ định. Kiểm tra dừng trước khi tải assembly/kết nối SQL; xem evidence/powershell-export-parameters.json. Export metadata trên Windows vẫn chờ kết quả người dùng.

Người dùng đã báo bộ test ứng dụng sau sửa builder: tổng 31, thành công 31, lỗi 0, bỏ qua 0. Sau đó đã nhận và đọc TRX chi tiết, xác nhận đủ 31 bản ghi Passed; xem evidence/ProductApi_Windows_31_Passed.trx. Bộ Account/Cart mới có 62 test như ACCOUNT_CART.md.
