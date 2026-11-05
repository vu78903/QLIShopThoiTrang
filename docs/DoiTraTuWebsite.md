# Yêu cầu đổi / trả từ website — local

Web → chi tiết đơn hoàn thành → đổi/trả. Khách chọn màu, size trong cùng mặt hàng và hai số lượng riêng (gửi lại / nhận mới). API web ghi `phieudoitra` / `chitietdoitra`, `NguonDon=WEB`, trạng thái `CHO_DUYET`. Hai ứng dụng phải dùng cùng database.

Form → **Đổi trả → Yêu cầu từ website → Xem yêu cầu** hiển thị sản phẩm cũ/mới, màu/size, số lượng, người liên hệ, số điện thoại, địa chỉ đổi và thông tin nhận hoàn đã lưu. Chỉ cửa hàng trưởng nhận số tài khoản đầy đủ qua API; vai trò khác chỉ nhận bốn số cuối.

Duyệt kiểm tra lại `MaSP` cùng sản phẩm, lượng còn được trả và tồn thực tế. Lượng hàng đổi phải bằng lượng hàng gửi lại, không mua thêm trong yêu cầu đổi/trả; cả API web và API duyệt form đều chặn số lượng lệch. Không đổi sang mặt hàng khác dù có phiếu cũ/giả mạo hoặc khách sửa request. Hàng mới trừ kho theo hành vi duyệt hiện có; hàng cũ chỉ cộng kho sau **Nhận và kiểm hàng** xác nhận đạt. Phiếu đã duyệt có nút **Theo dõi nhận / kiểm hàng** lọc đúng phiếu.

`HOAN_TAT` ở phiếu là đã duyệt, không đồng nghĩa đã nhận hàng hoặc đã hoàn tiền. Web cho bổ sung tài khoản theo đúng phiếu đã duyệt có khoản hoàn chưa chi. Duyệt/kiểm hàng không tự chuyển tiền hoặc xác nhận đã thu thêm.

## Cấu trúc

`database/BoSung-ThongTinDoiTraWeb.sql` giống script web `Supabase-DoiTra-CungSanPham.sql`. Bổ sung nếu thiếu 3 cột người nhận/điện thoại/địa chỉ đổi và 4 cột tài khoản/ngày hoàn trên `phieudoitra`; không tạo bảng và không đổi tồn/tiền. API contract yêu cầu 3 cột liên hệ mới. Áp dụng script trên database local tương thích trước khi chạy API mới; script kiểm nhận `BoSung-KiemNhanDoiTra.sql` của lượt trước vẫn cần thiết.

Ngày 05/11/2026 sau yêu cầu “thêm và build”, đã áp dụng hai script kiểm nhận và thông tin đổi trả lên Supabase đang cấu hình: vẫn 18 bảng, không thêm bảng, không đổi tồn/tiền. Đã build bản chạy chính `winform/qlicuahang/bin/Debug/net8.0-windows/qlicuahang.exe` kèm API mới trong thư mục `api`. API form đang chạy cổng 5001 bằng runtime .NET 10 local đầy đủ của repo web. Nếu khởi động lại máy, chạy `tmp/start-return-services.ps1` ở repo web rồi mở form. Chưa commit/push.

## Kiểm chứng

151 kiểm tra database/concurrency/HTTP qua; 22 kiểm tra UI WebView đổi trả qua và 27 kiểm tra UI hồi quy qua. Repo web kiểm thử PostgreSQL thật với repository form được link vào: ngang tiền, thu thêm, hoàn chênh lệch, trả hoàn, chống gửi/duyệt lặp, từ chối khác sản phẩm hoặc hết tồn lúc duyệt, bank masking và kiểm nhận từng phần. Web Razor có 17 kiểm tra trình duyệt desktop/mobile riêng với API giả.
