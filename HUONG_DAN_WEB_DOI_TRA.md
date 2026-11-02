# Đồng bộ yêu cầu đổi trả web với form

Phía form/API đã nhận yêu cầu từ **PhieuDoiTra và ChiTietDoiTra**. Không tạo lại `return_request` hoặc bất kỳ bảng tiếng Anh nào. Website nằm trên máy khác nên bên web phải cập nhật chỗ ghi yêu cầu theo hợp đồng này trước khi kiểm thử xuyên suốt.

## Web ghi trong cùng một transaction

- Xác thực khách hàng và kiểm tra `HoaDon.MaKH` đúng người đăng nhập; không tin MaKH do trình duyệt gửi tùy ý.
- `PhieuDoiTra`: SoPhieuDT duy nhất (tối đa 30 ký tự); SoHD là hóa đơn gốc; MaNV = NULL; NguonDon = `WEB`; TrangThai = `CHO_DUYET`; HinhThucXuLy = `HOAN_TIEN`, `DOI_SAN_PHAM` hoặc `DOI_VA_TRA` khi có nhiều hình thức; MaYeuCau là mã gửi duy nhất, giữ nguyên khi gửi lại; AnhMinhChung là mảng JSON URL ảnh. Tiền do form/API tính khi duyệt, lúc gửi yêu cầu để các trường tiền bằng 0.
- `ChiTietDoiTra`: một dòng cho mỗi MaSKU trong yêu cầu; SoLuong, LyDoTra giữ đúng khách chọn; LoaiGiaoDich = `HOAN_TIEN` hoặc `DOI_SAN_PHAM`; MaSKUDoi và SoLuongDoi ghi đúng lựa chọn đổi mới, nếu hoàn tiền thì MaSKUDoi = NULL và SoLuongDoi = 0.
- NgayLap do hệ thống ghi. Không đặt MaNV giả để vượt ràng buộc. Không gửi yêu cầu thẳng với trạng thái `HOAN_TAT`.
- Gửi lại cùng thao tác phải dùng cùng SoPhieuDT/MaYeuCau và trả về yêu cầu cũ khi trùng khóa; không tạo thêm phiếu.

## Form tiếp nhận

- GET `/api/doi-tra/yeu-cau`: danh sách yêu cầu chờ và bị từ chối, cần token nhân viên.
- POST `/api/doi-tra/yeu-cau/{id}/duyet` với `{}`: API đọc lại toàn bộ SKU/số lượng/lý do từ database; không nhận SKU thay thế từ body để sửa yêu cầu. Nhân viên lấy từ phiên đăng nhập.
- POST `/api/doi-tra/yeu-cau/{id}/tu-choi` với `{ "reason": "Lý do" }`: lưu lý do từ chối.
- Duyệt giữ nguyên số phiếu yêu cầu, chuyển `HOAN_TAT` và ghi nhân viên xử lý. Gửi duyệt lặp trả lại cùng kết quả, không trừ kho/hoàn tiền lần hai.
- Form hiển thị chi tiết nhiều SKU và ảnh; người tiếp nhận chỉ duyệt hoặc từ chối, không tự chọn lại nhu cầu của khách.

## Quy tắc tiền đã triển khai

Giá sale SKU là giá tính tiền. Voucher hợp lệ và chiết khấu hạng khách được kiểm tra lại tại server. VAT hiện tại của project là 8%. Giá trị thực trả từng dòng được lưu ở `ChiTietHoaDon.GiaTriThucTra`, gồm phân bổ giảm giá và VAT, không gồm phí giao. Hóa đơn cũ chưa có cột giá trị được đọc theo tỷ lệ tiền hàng thực trả của hóa đơn gốc, không dùng chương trình sale hiện tại để định giá hàng trả.

Hoàn từng phần dùng giá trị lũy kế, giới hạn số lượng và tổng tiền đã hoàn. Hàng đổi mới áp dụng giá sale hiện hành cộng VAT; voucher cũ không được áp lại một lần nữa lên hàng đổi mới.

Đây chưa phải thay đổi luồng nhập lại kho hàng hoàn hoặc trừ kho COD lúc tạo đơn (mục 6 của tester).
