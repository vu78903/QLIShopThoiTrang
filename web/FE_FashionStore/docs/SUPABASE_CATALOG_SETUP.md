# Supabase: bước 1 — đọc sản phẩm và danh mục

Tài khoản khách hàng và giỏ hàng đã có chế độ bật riêng. Xem [hướng dẫn bước 2](SUPABASE_CUSTOMER_CART_SETUP.md) để chạy SQL bổ sung và khởi động với `-EnableCustomerFeatures`. Phần bên dưới mô tả chế độ catalog mặc định.

Đã đối chiếu bản xuất schema public ngày 23/10/2026: 16 bảng, 160 cột.
Bản xuất cũ ghi `masp` là khóa chính, nhưng dữ liệu API thực tế ngày 25/10/2026
có nhiều dòng cùng `masp`. Không dùng bản xuất cũ để kết luận khóa của database hiện tại.
Mapping chỉ đọc dùng `masku` làm định danh dòng (bản xuất cũ cũng xác nhận SKU duy nhất).
Danh sách gộp theo `masp` trước khi đếm/phân trang; chi tiết trả các SKU size/màu đang hoạt động.
Không tạo bảng, chạy migration, nhập dữ liệu hoặc tự động thay đổi database.

## Chạy thử

1. Dùng chuỗi kết nối PostgreSQL hiện hành do nhóm cung cấp (đổi mật khẩu đã lộ trong chat).
   Không lưu mật khẩu vào source hoặc gửi lại chat.
2. Mở PowerShell tại thư mục solution và chạy:

```powershell
.\scripts\Start-SupabaseCatalog.ps1
```

Script dùng `ConnectionStrings__Supabase` hoặc `YODYSTORE_CONNECTION_STRING`
đã lưu ở Process/User. Nếu chưa có, script hỏi chuỗi kết nối bằng ô nhập kín.
Dán chuỗi dạng `Host=...;Port=5432;Database=postgres;Username=...;Password=...;SSL Mode=Require`.
Ký tự mật khẩu có ý nghĩa đặc biệt trong connection string như dấu chấm phẩy cần được quote đúng.
Thông tin chỉ đưa vào môi trường của phiên chạy, không ghi file hay lưu lại User.
Nhấn Ctrl+C để dừng backend. Script khôi phục biến môi trường khi thoát.

3. Mở lần lượt:
   - http://127.0.0.1:5000/health/live — backend chạy.
   - http://127.0.0.1:5000/health/ready — kết nối database; không xác nhận schema.
   - http://127.0.0.1:5000/api/categories — đọc danh mục hoạt động.
   - http://127.0.0.1:5000/api/products — đọc sản phẩm và danh mục hoạt động.
   - http://127.0.0.1:5000/swagger — thử API với bộ lọc.
4. Chạy FashionStore.Web trong Visual Studio, giữ API URL tại cổng 5000.
   Mở `/Product` và một trang chi tiết; đối chiếu tên, giá, SKU, size, màu và tồn kho.

## Cấu hình

Mặc định vẫn là SQL Server để không vô tình thay môi trường đang dùng.
Script đặt `Database__Provider=Supabase` cho lần chạy này.
Khi chạy bằng cách khác, đặt `Database__Provider=Supabase` và
`ConnectionStrings__Supabase` (ưu tiên) hoặc `YODYSTORE_CONNECTION_STRING`.
Không dùng chuỗi PostgreSQL ở `ConnectionStrings__FashionStore`: đó là cấu hình SQL Server cũ.

## Phạm vi và giới hạn

- Có tìm kiếm, lọc danh mục/size/màu/giá/tồn kho, phân trang, chi tiết và SKU.
- Dùng `soluongtonkho` làm tồn kho, không cộng với `soluong`.
- Giá sản phẩm là giá thấp nhất và tồn kho là tổng của các SKU đang hoạt động.
  Tất cả bộ lọc size/màu/giá/tồn kho phải cùng khớp trên một SKU; sản phẩm khớp
  vẫn hiển thị giá/tồn của toàn bộ SKU đang hoạt động, giống catalog SQL Server.
- Không có ngày tạo: API trả `createdAt=null`; sort `new` tương thích link cũ
  nhưng sắp xếp ổn định theo mã, giao diện danh sách ghi “Mặc định”.
- Không có đối tượng: API trả `audience=null`, từ chối bộ lọc đối tượng bằng HTTP 400.
  Giao diện danh sách ẩn bộ lọc khi nhận dữ liệu Supabase; các link nam/nữ cũ
  từ phần điều hướng vẫn cần chuyển sau khi nhóm thống nhất phân loại.
- `hinhanh` tạm được coi là một đường dẫn ảnh. Chưa xác minh định dạng thực tế
  hay khả năng truy cập đường dẫn từ website.
- Các controller còn lại (đăng nhập, giỏ, thanh toán, quản trị...) trả HTTP 503
  trong chế độ Supabase, trước khi xác thực hoặc khởi tạo service SQL Server.
- Không suy đoán định dạng mật khẩu `nhanvien.matkhau`.
- Checkout còn cần thống nhất nhân viên phụ trách (`hoadon.manv` bắt buộc),
  trạng thái, trừ/hoàn tồn kho, và function/trigger hiện có.
- Cần bổ sung tài khoản khách hàng, giỏ hàng và các tính năng khách hàng
  sau khi thiết kế script tương thích với database chung.

## Kiểm tra

```powershell
dotnet test .\FashionStore.Backend.Tests\FashionStore.Backend.Tests.csproj -c Release
```

Các test Supabase hiện kiểm tra dịch truy vấn PostgreSQL, ánh xạ SKU và chặn
endpoint chưa chuyển. Chúng không xác nhận dữ liệu thật hay giao diện chạy thực tế.
Các test SQL Server tích hợp được bỏ qua nếu không cấu hình database test riêng.

Kết quả kiểm tra ngày 23/10/2026:
- Backend Release build thành công (còn cảnh báo CS0108 có sẵn ở RecoveryController).
- 75 test đạt, 15 test tích hợp SQL Server bỏ qua, 0 lỗi.
- MVC Debug build và compile Razor (`MvcBuildViews=true`) thành công.
- Script PowerShell đã kiểm tra cú pháp; chưa chạy kết nối thật.
- Chưa có biến kết nối Supabase ở Process/User tại thời điểm kiểm tra.
- Chưa kiểm tra ảnh, dữ liệu thực tế hoặc trình duyệt; không thay đổi database từ xa.

Driver: https://www.npgsql.org/efcore/index.html
Kết nối Supabase: https://supabase.com/docs/guides/database/connecting-to-postgres

Kiểm tra và sửa ngày 25/10/2026:
- Kết nối Supabase thật thành công, API danh mục trả 6 mục.
- API danh sách cũ trả 485 dòng SKU, không phải 485 mã sản phẩm khác nhau.
- Sửa danh sách lặp mã, tổng đếm, phân trang và chi tiết nhiều SKU.
- 9/9 test Supabase đạt: dịch truy vấn với PostgreSQL provider và hồi quy trên
  dữ liệu in-memory có nhiều SKU, lọc đồng thời, SKU ẩn, danh mục ẩn, hết hàng,
  giá/tổng tồn, thứ tự trang và chi tiết không tồn tại.
- Chưa chạy bản sửa trên Supabase thật: cần dừng backend cũ và chạy lại script,
  nhập kết nối hiện hành, rồi thử `/api/products`, `/api/products/SP-001`
  và `/api/products/SP-001/variants`. Không thay đổi database từ xa.
