# Prompt bàn giao cho AI tiếp theo

Hãy tiếp tục sửa và kiểm thử project QLIShopThoiTrang đến khi hoàn thành 10 mục bên dưới. Đọc mã nguồn hiện tại trước khi sửa, giữ lại phần đã hoàn thành, không viết lại tùy tiện. Làm việc trong repo thật, không sửa nhầm bản project cũ.

## Bối cảnh và giới hạn

- Repo trên máy hiện tại: `C:\Users\Anh Vu\OneDrive\Desktop\KLCN\QLIShopThoiTrang`. Workspace chat cũ `...\KLCN\qlicuahang` là thư mục khác và có cả bản code cũ.
- Nhánh main; remote `https://github.com/vu78903/QLIShopThoiTrang.git`.
- WinForms WebView2: `winform/qlicuahang`; API .NET 10: `api/QLIShopThoiTrang.Api`. **Toàn bộ form và API dùng http://127.0.0.1:5001.** Web trên máy khác, không tự sửa hoặc xóa website ngoài phạm vi.
- Public đã được người dùng yêu cầu dọn hết bảng tiếng Anh. Chỉ dùng bảng tiếng Việt hiện có; đặc biệt không tạo lại `return_request`, `customer_account`, `order_snapshot` hoặc bảng tiếng Anh khác. Có thể bổ sung cột tiếng Việt cần thiết vào bảng hiện có.
- Public giữ 18 bảng: baocaodoanhthu, bienbanxulyhangloi, chitietbaocaodoanhthu, chitietdoitra, chitiethoadon, chitietnhapkho, giaodichthanhtoanqr, hoadon, khachhang, khuyenmai, khuyenmaisanpham, loaisanpham, nhanvien, nhasanxuat, phanquyen, phieudoitra, phieunhapkho, sanpham; view `sanphamkhuyenmai_hientai`.
- Trigger hoàn tiền web `capture_cancelled_order_payment` đã được người dùng cho phép vô hiệu hóa vì các bảng nó gọi đã bị xóa. Giữ trigger bảo vệ lịch sử `guard_cancelled_web_order` và kiểm tra nhà cung cấp nhập kho. Không bật lại trigger gọi bảng không còn tồn tại.
- Bốn quyền: cửa hàng trưởng = admin, trưởng ca, thu ngân, nhân viên kho. Chỉ admin quản lý Đối tác. Giữ các kiểm tra token/quyền hiện tại.
- Giữ phân loại Nam/Nữ/Phụ kiện, nhóm MaSP và SKU, nhiều ảnh/SKU, phụ kiện Free size, size quần 28–38. Phiếu nhập tính kiện, 1 kiện = 50 cái; tồn sản phẩm tính cái.
- Không commit mật khẩu/key/env, dữ liệu backup riêng hoặc dữ liệu khách hàng thật. Không đưa các file website bị xóa ngoài phạm vi vào commit của mình. Có thể tạo dữ liệu thử có tiền tố rõ ràng và phải dọn sạch sau kiểm thử.

## Phần đã sửa ở lượt trước: 1–5 phía form/API

1. POS hiện tồn khả dụng ngay trong giỏ; chọn voucher từ danh sách đủ điều kiện, vẫn cho nhập mã; không lẫn chương trình giảm SKU.
2. `ProductDto` có SalePrice/SalePercent/SaleCode. Giá gốc vẫn giữ trong Price; giỏ dùng giá sale. API kiểm tra cùng giá sale, voucher phần trăm/số tiền, hạng khách và VAT. SKU tester báo `1-023-DEN-S` trước đây POS gửi giá gốc nên bị từ chối.
3. Khóa thanh toán và bước hoàn tất, mở lại khi lỗi/hủy. Client giữ cùng mã hóa đơn khi gửi lại cùng một lần thử. Server khóa theo mã hóa đơn, lưu `HoaDon.DauVanTayYeuCau`, xử lý lại cùng nội dung không tạo hóa đơn/trừ tồn lần hai; cùng mã khác nội dung bị từ chối.
4. Lưu `ChiTietHoaDon.GiaTriThucTra`. Đổi trả phân bổ giá thực trả gồm giảm giá/VAT, không gồm phí ship; hoàn từng phần có giới hạn và chống lặp; hỗ trợ hóa đơn cũ chưa có snapshot. Hàng đổi mới dùng sale hiện hành cộng VAT. Không áp lại voucher cũ lên hàng mới.
5. Danh sách yêu cầu web đọc từ `PhieuDoiTra/ChiTietDoiTra`, hiển thị đúng nhiều SKU, số lượng, lý do, SKU/qty muốn đổi và ảnh. Nhân viên chỉ duyệt/từ chối. API đọc lại dữ liệu gốc, lấy nhân viên từ token, khóa giao dịch và chống duyệt lặp. **Phía website trên máy kia chưa được sửa trong lượt này**: phải thực hiện hợp đồng trong `HUONG_DAN_WEB_DOI_TRA.md`, rồi mới xác nhận mục 5 hoàn thành xuyên suốt.

38 kiểm tra đã qua ở lượt trước: 23 API/Supabase và 15 UI/Playwright. Có kiểm tra thanh toán đồng thời, retry, tính sale+voucher+hạng khách, hoàn hai lần từng phần đúng tiền gốc, duyệt nhiều SKU đồng thời và lịch sử không nhân đôi. Đây không phải xác nhận toàn bộ ứng dụng đã hết lỗi.

File chính: `StoreRepository.Pricing.cs`, `StoreRepository.ReturnRequests.cs`, `StoreRepository.cs`, `SqlServerDatabaseInitializer.cs`, `StoreModels.cs`, `Program.cs`; JS `ung-dung.js`, `pos-doi-tra.js`; Views `ban-hang.html`, `doi-tra.html`. Các view khác cũng nạp helper dùng chung. Migration chỉ thêm cột vào bảng tiếng Việt; MaNV của phiếu chờ có thể NULL nhưng phiếu hoàn tất phải có nhân viên.

Kiểm thử tái lập: xem `tests/pos-returns/README.md`, `api.mjs`, `ui.mjs`, SQL setup/cleanup và DB helper. Kết quả/screenshot ở `artifacts/pos-returns` khi chạy theo hướng dẫn; các lượt chạy ở chat cũ lưu thêm trong workspace cũ `pos-fixes-tests`.

## Tiếp tục hoàn thành cả 10 mục

1–4: chạy hồi quy, kiểm tra các góc cạnh và sửa lỗi còn sót; không làm mất phần đã hoàn thành.

5. Cập nhật bên web để ghi yêu cầu vào đúng hai bảng tiếng Việt theo tài liệu, rồi thử từ khách gửi đến nhân viên duyệt/từ chối. Không cho nhân viên chọn đại SKU/lý do thay khách. Kiểm tra quyền sở hữu hóa đơn ở backend web. Không giả lập rồi báo đã đồng bộ thật.

6. Trừ/giữ tồn khả dụng ngay khi tạo đơn giao hàng, kể cả COD, và chỉ một lần. Hiện đơn trả trước đã trừ khi tạo nhưng COD còn đợi giao thành công. Tách ghi nhận thanh toán/doanh thu khỏi trừ kho. Không bulk trừ lại đơn cũ khi chưa biết chúng đã được trừ hay chưa. Hàng hủy/đổi/trả cần danh sách chờ kiểm: hàng đạt được duyệt mới cộng lại tồn; hàng lỗi/thanh lý phải tách riêng. Hủy trước khi xuất hàng xử lý lượng đã giữ, không cộng kho hai lần. Mọi duyệt phải chống lặp và tranh chấp giữa hai máy.

7. Hoàn thiện tài khoản khách hàng: dùng nguồn cột tiếng Việt trong KhachHang, thống nhất với web; chỉ admin sửa. Form đã có username/password và nút hiện/ẩn. Trước khi dọn DB, nguồn web khác bảng nên KhachHang trống. Không tự điền mật khẩu giả hoặc hứa khôi phục mật khẩu gốc từ hash. Không xuất thông tin thật vào log/Git.

8. Chống double click trên tất cả thao tác ghi: CRUD, duyệt/từ chối nhập kho, đổi trả, trạng thái SKU, thanh toán/hoàn tiền... Khóa nút trong thời gian xử lý, mở lại khi lỗi và có bảo vệ xử lý lặp phía server. Không chỉ sửa một nút POS.

9. Rà toàn bộ hộp xác nhận xóa. Modal đang dùng chung z-index, hộp mở sau có thể đè lên hộp xác nhận đã tạo trước. Đảm bảo xác nhận ở trên cùng, focus đúng, không bấm xuyên xuống nền; kiểm tra modal lồng nhau và đóng/hủy.

10. Hoàn thiện nghiệp vụ thanh lý, hiện chưa có màn hình/API hoàn chỉnh. Ưu tiên tận dụng BienBanXuLyHangLoi và các bảng tiếng Việt hiện có: hàng nguồn, SKU/qty, lý do, giá, người lập/duyệt, trạng thái, chứng từ. Kiểm tra không bán vượt lượng hàng thanh lý hoặc ghi/trừ kho hai lần; tách hàng lỗi/chờ thanh lý khỏi hàng thường. Giữ đúng phân quyền.

## Cách bàn giao

Build WinForms/API và kiểm thử bằng thao tác thật cùng dữ liệu thử. Báo rõ trước/sau, trường hợp đạt/chưa đạt, ảnh minh chứng, giới hạn cần phối hợp máy web. Dọn hết dữ liệu thử và kiểm chứng số dòng còn lại bằng 0. Không xóa chứng từ thật. Sau khi đạt, commit/push main các file đúng phạm vi và gửi mã commit cùng lệnh lấy bản mới. Không tự khẳng định đủ 10 mục nếu phần web hoặc nghiệp vụ chưa được kiểm chứng.
