# Nguồn mapping và giới hạn

Phần đầu ghi nguồn của checkpoint Product API ban đầu. Mapping hiện tại đã được đối chiếu với export DEV thật; xem phần cập nhật Account/Cart ở cuối và docs/ACCOUNT_CART.md.

Tiếp tục theo đề xuất FashionStoreDB v2 sau khi người dùng yêu cầu “Tiếp tục”. Trong lượt này đã đọc được **nội dung đầy đủ, phiên bản 1, 505 dòng** của `01_Create_FashionStoreDB.sql` đã có từ trước. Cùng bộ migration v2 hiện được đính kèm, nguồn này đủ để viết mapping catalog có căn cứ; không còn phải suy đoán tên cột cho bốn bảng.

**Chưa chứng minh schema trong file backup trùng các nguồn DDL này.** SQL Server 2022 không khởi động được trong môi trường thực thi, nên backup chưa restore và YodyStore chưa đối chiếu. Không tạo database thay thế từ DDL v1 để biến kiểm thử giả thành kiểm thử backup.

| Entity | Nguồn và phạm vi mapping |
|---|---|
| SanPham | DDL gốc: MaSP varchar(20), MaLoai varchar(20), TenSanPham nvarchar(200), MoTa nvarchar(max) nullable, DonGiaNiemYet decimal(18,2), TrangThai bit, NgayTao datetime2(0). Migration v2 bổ sung DoiTuong varchar(10) nullable. |
| LoaiSanPham | DDL gốc: MaLoai varchar(20), TenLoai nvarchar(100), TrangThai bit. MoTa không cần trả ra catalog nên không map. |
| BienTheSanPham | DDL gốc: MaSKU varchar(40), MaSP varchar(20), KichCo nvarchar(30), MauSac nvarchar(60), DonGia decimal(18,2), SoLuongTonKho int, TrangThai bit. Không dùng MaSP làm SKU; MaVach/RowVer không cần cho API đọc này. |
| HinhAnhSanPham | DDL gốc: MaHinh bigint identity, MaSP varchar(20), MaSKU varchar(40) nullable, DuongDan nvarchar(500), AnhChinh bit, ThuTu int. |

Đây là mapping **subset để đọc database hiện hữu**, không phải scaffold chạy trên backup. Không có EnsureCreated, Migrate, EF migration hay tự cài procedure. `Verify-CatalogSchema-ReadOnly.sql` sẽ kiểm tra metadata trên bản restore Windows trước khi nghiệm thu.

Giá ở list/detail được tính bằng minimum DonGia của SKU hoạt động, không lấy giá niêm yết thay cho giá SKU. Product không có SKU hoạt động không xuất hiện. Trường null DoiTuong vẫn null, không tự đoán giới tính. Tồn tổng dùng long để không tràn int khi cộng nhiều SKU; tồn từng SKU giữ int đúng DDL.

## Giới hạn của checkpoint Product API ban đầu (lịch sử)

- DDL đầy đủ/metadata và số đối tượng thực tế của hai backup, including procedure đã cài. File RC không tự chứng minh backup đã được audit.
- SQL integration với dữ liệu đã restore; bảy test thật được cung cấp nhưng đang BLOCKED.
- Razor/IIS Express, HTTP giữa hai process Windows và hình ảnh thực tế trong DB.
- `.NET 8` hết hỗ trợ ngày 10/11/2026. Giữ VS2022 cho checkpoint này; nâng backend dài hạn cần chốt toolchain .NET 10 phù hợp.
- Bộ procedure RC khóa DB_NAME là FashionStoreDB. Product API chưa gọi chúng. Trước module ghi cần kiểm tra định nghĩa đã cài và phương án test DEV, không đổi tên DB để lách guard.
- Return handler RC chỉ một dòng SKU/phiếu và tính 15 ngày từ NgayLap hóa đơn. Chưa tự mở rộng hoặc thay chính sách.
- Banner điểm, nút tài khoản/giỏ hàng không có nghĩa module đó đã được triển khai.

Checkpoint Product API ban đầu chờ kết quả SQL/MVC. Sau đó đã nhận TRX 31/31 PASS và ảnh List/Details; Account/Cart hiện được bổ sung theo export thật. Checkout tiếp tục chờ nghiệm thu module mới và phương án SP trên DEV.

## Cập nhật Account/Cart từ metadata thật

Đã nhận export đầy đủ từ FashionStoreDB_Dev, SQL Server 16.0.1000.6, compatibility 160, collation Vietnamese_CI_AS. Đếm theo đối tượng: 29 bảng, 2 view, 47 FK, 6 SP; foreignKeys có 48 dòng do một khóa composite. Tất cả định nghĩa module có thể đọc, không có constraint disabled/untrusted theo metadata. Snapshot byte-for-byte được giữ trong database/schema-reference. Các cột/PK của cả tám entity được kiểm tra tự động với snapshot; kiểm tra này không thay audit dữ liệu nghiệp vụ hoặc hai backup.

Account/Cart bổ sung map KhachHang, TaiKhoanKhachHang, GioHang và ChiTietGioHang. Không có bảng địa chỉ riêng hoặc session riêng, không tự thêm schema. Chính sách và giới hạn phiên/địa chỉ/hash được ghi tại docs/ACCOUNT_CART.md. TRX Windows 31/31 cho Product API đã được đọc; bước SQL/UAT mới tiếp tục với 62 test.
