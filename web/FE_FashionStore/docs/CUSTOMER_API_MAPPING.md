# Customer API mapping — Task 3

| MVC / UI | API | Service / SQL | Quyền / ghi chú |
|---|---|---|---|
| Home, Product | GET api/products; GET api/categories; GET api/products/{id} | CatalogService / SanPham, BienTheSanPham, LoaiSanPham, HinhAnhSanPham | Public; lọc SQL trước COUNT/paging; thêm minPrice,maxPrice,size,color,inStock |
| Account/Login,Register,Profile,Password | api/account/* giữ nguyên | AccountService / KhachHang, TaiKhoanKhachHang | Unified login Customer/Admin giữ nguyên |
| Cart | api/cart; api/cart/items/{sku} | CartService / GioHang, ChiTietGioHang | Customer; thêm ImageUrl response, không tin giá từ client |
| Checkout | POST api/orders | OrderService / HoaDon, ChiTietHoaDon, ThanhToan, KhuyenMai | Customer; Voucher optional, RequestId giữ nguyên; transaction/row locks |
| Checkout kiểm tra ưu đãi | GET api/orders/quote?voucher= | OrderService / cart, KhuyenMai | Customer; preview, đặt hàng luôn tính lại |
| Orders | GET api/orders?q=&status=&page= | OrderService / HoaDon | Customer; 100/trang, lọc SQL |
| Orders/Details | GET api/orders/{id} | OrderService / HoaDon, ChiTietHoaDon | Customer; thêm Subtotal,Discount,Voucher |
| Orders/Cancel | POST api/orders/{id}/cancel | OrderService / usp_HuyDonOnline | Customer; khóa kiểm tra chủ sở hữu cùng transaction với procedure |
| Tiến trình đơn | GET api/customer/orders/{id}/history | CustomerFeaturesService / LichSuTrangThaiDonHang,HoaDon | Customer; chỉ đơn thuộc tài khoản |
| Customer/Promotions | GET api/customer/promotions | CustomerFeaturesService / KhuyenMai | Public; active/date/usage checks |
| Customer/Points | GET api/customer/points?page= | CustomerFeaturesService / LichSuDiem | Customer; 20/trang |
| Customer/Returns | GET/POST api/customer/returns | CustomerFeaturesService / PhieuDoiTra,ChiTietDoiTra,HoaDon,lịch sử | Customer; 20/trang, ownership, 15 ngày, cumulative quantity; pending only |
| Customer/Reviews/{id} | GET api/customer/reviews/{id}?page= | CustomerFeaturesService / DanhGiaSanPham,SanPham | Public; chỉ HIEN_THI; 10/trang; tên hiển thị không lộ thông tin cá nhân |
| Gửi đánh giá | POST api/customer/reviews | CustomerFeaturesService / đơn hoàn tất + chi tiết + DanhGiaSanPham | Customer; không đánh giá trùng cùng sản phẩm/đơn; CHO_DUYET |
| Customer/Wishlist | GET api/customer/wishlist; PUT/DELETE api/customer/wishlist/{id} | CustomerFeaturesService / WebWishlist,SanPham | Customer; migration 06 |
| Customer/Addresses | GET/POST api/customer/addresses; DELETE api/customer/addresses/{id} | CustomerFeaturesService / WebAddress | Customer; migration 06, tối đa 20; Checkout chỉ đọc địa chỉ đúng tài khoản |
| Account/Forgot | POST api/account/recovery | PasswordRecoveryService / WebPasswordReset,KhachHang,TaiKhoanKhachHang | Anonymous + rate limit; SMTP thật, thông báo không tiết lộ tài khoản |
| Account/Reset | POST api/account/recovery/reset | PasswordRecoveryService / WebPasswordReset,TaiKhoanKhachHang | Anonymous + rate limit; hash token, expiry, dùng một lần, transaction |
| Chính sách/liên hệ | Không cần API | Razor + appSettings liên hệ/ngân hàng | Public, không bịa cấu hình |

MVC dùng server-to-server API; không đưa bearer token vào JavaScript. Mọi form ghi dùng AntiForgeryToken. SQL mới dùng parameter, không ghép giá trị user vào câu lệnh. Admin-specific API/controller/views không sửa. Các dictionary response của api/customer giữ tên cột gốc; MVC dùng JArray/JToken và Razor encode nội dung.

Ví dụ body đổi trả: `{"requestId":"<GUID sinh từ form>","orderId":"<đơn thật>","sku":"<SKU trong đơn>","quantity":1,"kind":"TRA","reason":"Mô tả thực tế"}`. Đổi hàng thêm `newSku`, kind=DOI. Không gửi tổng tiền, trạng thái hay MaKH từ browser.

Swagger runtime sinh từ controllers là nguồn API mới; file docs/openapi.v1.json cũ thuộc Task 2, chưa được tái sinh vì không chạy được API trong môi trường này.
