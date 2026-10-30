# Supabase: bước 2 — tài khoản khách hàng và giỏ hàng

Bước tiếp theo cho đơn COD có trong [hướng dẫn đơn website](SUPABASE_WEB_ORDERS_SETUP.md). Nội dung dưới đây mô tả chế độ tài khoản/giỏ hàng chưa bật đơn.

Code đã hỗ trợ bước này, nhưng cần chạy SQL bên dưới trước khi bật tính năng.
Đây là tài khoản website do backend quản lý, không phải Supabase Auth hay tài khoản nhân viên WinForms.

## 1. Chuẩn bị database dùng chung

Mở `database/Supabase-Customer-Cart.sql`, sao chép toàn bộ nội dung vào SQL Editor của đúng dự án Supabase và chạy bằng role chủ database (`postgres`).

Script thực hiện trong một transaction:

- Thêm cột `public.khachhang.nhankhuyenmai` với mặc định false.
- Bổ sung ràng buộc duy nhất cho số điện thoại và email không rỗng (email bỏ khoảng trắng đầu/cuối, không phân biệt hoa thường). Ràng buộc này cũng áp dụng khi WinForms ghi dữ liệu.
- Tạo ba bảng riêng `webstore.customer_account`, `webstore.cart`, `webstore.cart_line`.
- Liên kết khách hàng theo `makh`, sản phẩm theo `masku`; mật khẩu chỉ lưu dạng hash.
- Bật RLS và thu hồi quyền truy cập các bảng riêng từ PUBLIC, anon và authenticated. Backend truy cập qua kết nối PostgreSQL của chủ database, không đọc chúng trực tiếp từ trình duyệt.

Script không xóa khách hàng, sản phẩm, hóa đơn hoặc sửa tồn kho. Nếu dữ liệu trùng email/số điện thoại hoặc schema không phù hợp, transaction dừng; cần xem lỗi và đối chiếu với partner trước khi xử lý dữ liệu. Không tự xóa dữ liệu để chạy tiếp. Nếu SQL Editor còn báo transaction bị hủy, chạy `ROLLBACK;` trước khi thử lại.

Không chạy script trên một schema `webstore` đang phục vụ ứng dụng khác mà chưa đối chiếu quyền và cấu trúc. Script có thể chạy lại trên cấu trúc đúng mà nó tạo ra; không tự sửa các bảng có sẵn khác cấu trúc.

## 2. Khởi động backend

Dừng backend đang chạy bằng Ctrl+C, sau đó tại thư mục dự án:

```powershell
.\scripts\Start-SupabaseCatalog.ps1 -EnableCustomerFeatures
```

Script lấy chuỗi kết nối hiện hành từ môi trường hoặc hỏi nhập kín. Nhập toàn bộ chuỗi kết nối khi được hỏi, không chỉ mật khẩu. Không đưa mật khẩu vào source code.

Khi chạy backend bằng công cụ khác, cần đặt `Database__Provider=Supabase`, `Database__EnableSupabaseCustomerFeatures=true` và chuỗi kết nối `ConnectionStrings__Supabase` hoặc `YODYSTORE_CONNECTION_STRING`.

Không có switch này, backend chỉ bật catalog. Bỏ switch không xóa các bảng hoặc dữ liệu đã tạo.

## 3. Kiểm tra trên website

1. Mở `/health/ready` của backend: cần trả `database-connected`. Kết quả này chỉ xác nhận kết nối, không xác nhận đã chạy script.
2. Chạy FashionStore.Web, đăng ký một tài khoản thử với số điện thoại/email chưa tồn tại.
3. Đăng nhập, cập nhật hồ sơ và thử đổi mật khẩu. Đổi mật khẩu sẽ yêu cầu đăng nhập lại.
4. Chọn màu và size của sản phẩm, thêm giỏ hàng, đổi số lượng, xóa sản phẩm.
5. Đăng nhập tài khoản khác để kiểm tra giỏ riêng. Giá và tồn kho được đọc lại từ sản phẩm hiện hành; thêm giỏ chưa trừ hoặc giữ tồn kho.

Thanh toán vẫn khóa trong chế độ Supabase. Đơn hàng, khuyến mãi, khôi phục mật khẩu và quản trị chưa được chuyển trong bước này.

## Tài khoản và dữ liệu cũ

- Tài khoản SQL Server cũ chưa được nhập sang PostgreSQL.
- Khách hàng đã có số điện thoại/email trong dữ liệu WinForms không được tự nhận tài khoản bằng cách đăng ký lại. Cần bổ sung quy trình xác minh và liên kết tài khoản ở bước sau.
- Phiên đăng nhập hiện lưu trong bộ nhớ backend; khởi động lại backend sẽ yêu cầu đăng nhập lại.
- Hồ sơ sử dụng phiên bản dòng PostgreSQL `xmin` để phát hiện cập nhật đồng thời, kể cả thay đổi từ WinForms.

## Kiểm chứng đã thực hiện

Đã chạy migration hai lần và thử API trên PostgreSQL cục bộ riêng: đăng ký, chống trùng tài khoản, đăng nhập/đăng xuất, cập nhật hồ sơ và xung đột phiên bản, đổi mật khẩu, giỏ hàng theo chủ sở hữu, giá/tồn kho thay đổi, sản phẩm ngừng bán, cập nhật đồng thời. Đây không phải kết quả kiểm thử ghi vào Supabase dùng chung.

Bộ kiểm thử: 80 thành công, 15 kiểm thử SQL Server bỏ qua; MVC và Razor biên dịch thành công.
