# Kết quả sửa lỗi 6–9 — 03/11/2026

Phạm vi: WinForms/WebView và API tại cổng 5001. Mục 10 thanh lý giữ lại, chưa triển khai. Không sửa website và không tạo thêm bảng.

| Mục | Trước khi sửa | Sau khi sửa |
|---|---|---|
| 6 — Giữ tồn | COD chỉ trừ tồn khi giao thành công | Đơn COD và giao trả trước trừ tồn trong transaction tạo hóa đơn; giao thành công không trừ lần hai |
| 6 — Hủy | Xóa COD xóa cả hóa đơn và chi tiết | Hủy giữ chứng từ, chuyển sang danh sách đơn hủy; chưa tự cộng tồn |
| 6 — Hàng về | Thiếu danh sách kiểm nhận hủy/đổi/trả | Tồn kho có danh sách theo chứng từ/SKU, bộ lọc nguồn và kết quả. Cửa hàng trưởng xác nhận hàng thực nhận, ghi tình trạng, duyệt nhập lại hoặc không nhập lại. Quyết định chỉ tác động tồn một lần |
| 7 — Tài khoản khách | Chưa kiểm chứng trọn luồng sửa tài khoản khách | Xác minh luồng giống nhân viên: đọc tài khoản/mật khẩu hiện tại, Hiện/Ẩn, sửa trực tiếp, Lưu và mở lại đúng dữ liệu Supabase. Chỉ admin được đọc/sửa thông tin đăng nhập |
| 8 — Bấm lặp | Nhiều nút lưu/duyệt/chuyển trạng thái chưa khóa; duyệt nhập có thể cộng tồn lặp | Khóa thao tác đến khi xong hoặc lỗi; gộp request đang gửi; API nhận X-Request-ID để phát lại kết quả cùng yêu cầu; phiếu nhập có mã yêu cầu lưu DB, duyệt nhập/kiểm nhận/giao COD dùng khóa hàng và transaction |
| 9 — Xác nhận xóa | Các hộp cùng z-index có thể che nhau | Hộp xác nhận dùng dialog modal ở top layer của trình duyệt, hỗ trợ Escape và hủy. Kiểm thử cả khi một lớp khác có z-index 2147483647 mở sau |

## Cách sử dụng

- **Tồn kho → Kiểm nhận hàng hủy / đổi / trả**: chọn nguồn và trạng thái; bấm Kiểm nhận, ghi tình trạng, xác nhận đã nhận đủ hàng thực tế, chọn kết quả và duyệt. Không nhận hàng thì tiếp tục để chờ, không duyệt nhập lại.
- **Tồn kho → Đơn cũ cần đối chiếu tồn kho**: chỉ cửa hàng trưởng đối chiếu chứng từ cũ không rõ đã xuất kho. Nếu đã trừ trước đây thì chỉ đánh dấu; nếu chưa trừ thì đơn đang giao mới xuất tồn, đơn đã hủy không xuất tồn. Không tự đoán và chỉnh tồn của chứng từ cũ.
- **Đối tác → Khách hàng → Sửa**: ô mật khẩu chứa giá trị hiện tại như form nhân viên; nút Hiện/Ẩn; sửa trực tiếp rồi Lưu. Tài khoản chưa có thông tin đăng nhập vẫn để trống, không sinh mật khẩu giả.
- Hủy đơn đã thu tiền giữ nguyên số tiền và chứng từ. Kiểm nhận hàng không phải thao tác hoàn tiền; không tự chuyển tiền cho khách.

## Kiểm thử

Đã chạy bản API cuối nối Supabase thật bằng dữ liệu TESTPOS/TEST69 và Playwright/Edge:

| Nhóm | Số kiểm tra đạt |
|---|---:|
| Hồi quy API mục 1–5 | 23 |
| Hồi quy giao diện POS/đổi trả | 15 |
| API mới: tồn, hủy, kiểm nhận, đối chiếu cũ, tài khoản, gửi lặp | 42 |
| Giao diện mới: kiểm nhận, mật khẩu, khóa nút, xác nhận trên cùng | 27 |
| Giao diện nối API/Supabase thật: duyệt nhập lại và sửa mật khẩu | 5 |
| **Tổng** | **112** |

Các tình huống gồm hai yêu cầu đồng thời tranh hàng cuối; duyệt nhập/nhập lại hai lần; không cộng kho hàng lỗi; hủy giao trả trước; đơn hủy cũ không có trạng thái giao; bảo vệ tài khoản khách khỏi thu ngân; lưu lỗi rồi gửi lại; hộp xác nhận trên 9 màn hình.

Build WinForms và API: **0 lỗi, 0 cảnh báo**. Dữ liệu thử được dọn sau kiểm thử. Public vẫn giữ 18 bảng nghiệp vụ hiện có.

Kết quả JSON và ảnh được tạo trong `artifacts/pos-returns/` (không đưa lên Git):

- `api-results.json`, `ui-results.json`
- `fix69-api-results.json`, `fix69-ui-results.json`, `fix69-real-ui-results.json`
- `fix69-confirm-top.png`: xác nhận ở top layer.
- `fix69-real-stock.png`: kết quả kiểm nhận nối Supabase thật.
- `fix69-real-customer.png`: mở lại form khách hàng sau khi đổi mật khẩu, mật khẩu được che.

Giới hạn: kiểm tra giao diện HTML được WinForms sử dụng qua Edge/Playwright, không kiểm thử mọi tương tác hệ điều hành của cửa sổ WinForms gốc; không thực hiện chuyển khoản tiền thật. Kết nối website bên máy khác nằm ngoài phạm vi. Chống gửi lặp chung trong API giữ kết quả trong tiến trình; hóa đơn/phiếu nhập/đổi trả và tác động tồn còn có bảo vệ lưu DB.
