# Nhận và kiểm hàng đổi/trả

Màn hình **Đổi trả → Nhận và kiểm hàng đổi/trả** dùng cùng xử lý với **Tồn kho → Kiểm nhận**.

- Duyệt/tiếp nhận yêu cầu không tự cộng hàng cũ vào tồn bán được.
- Cửa hàng trưởng ghi số lượng thực nhận đã kiểm, lượng đạt để nhập kho và tình trạng. Phần chưa nhận vẫn chờ; phần không đạt không vào tồn bán.
- Mỗi lượt có mã riêng, kiểm tra phiên bản số lượng và transaction để chống bấm/gửi lặp hoặc hai người kiểm cùng dữ liệu cũ.
- Nhật ký giữ người xử lý, thời gian, lượng đã kiểm, lượng nhập lại và ghi chú. Quyết định đã lưu không bị sửa ngược.
- Màn hình hiển thị trạng thái tiền riêng. Kiểm hàng không chuyển tiền, xác nhận hoàn tiền, tính lại doanh thu hay xuất thêm hàng đổi mới.
- Giữ quyền hiện tại: chỉ cửa hàng trưởng được ghi kiểm nhận; các vai trò được đọc đổi trả có thể xem tại màn hình Đổi trả.

## Cấu trúc và triển khai

`database/BoSung-KiemNhanDoiTra.sql` bổ sung ba cột trên `chitietdoitra`: `soluongdakiemnhan`, `soluongnhaplai`, `nhatkykiemnhan`. Không thêm bảng. Chuyển kết quả kiểm nhận cũ sang số lượng tương ứng, giữ tồn kho nguyên vẹn và có thể chạy lại.

API mới yêu cầu ba cột trong schema contract. Startup chỉ kiểm tra, không tự chạy SQL. Sau yêu cầu “thêm và build” ngày 05/11/2026, đã áp dụng script này cùng script thông tin đổi trả lên Supabase đang cấu hình; vẫn 18 bảng, không thêm bảng, không đổi tồn/tiền. Đã build bản chạy chính và kiểm tra API kết nối thành công.

Không bao gồm tự hoàn tồn đơn hủy chưa giao, báo cáo doanh thu, thông báo web, chọn sản phẩm đổi trên web hoặc sửa phiếu in.

Lượt tiếp theo đã nối lựa chọn biến thể từ web vào phiếu duyệt và kiểm nhận; xem [Đổi trả từ website](DoiTraTuWebsite.md). Các giới hạn ở đoạn trên mô tả riêng phạm vi lượt kiểm nhận ban đầu.

## Kiểm chứng local ngày 05/11/2026

- API và WinForms build ở thư mục artifacts riêng: 0 lỗi, 0 cảnh báo. Không thay bản chạy chính.
- `version-a-checks`: 151 kiểm tra qua trên PostgreSQL local và HTTP thật, gồm 32 kiểm tra phân quyền. Fixture tự tạo/xóa database riêng.
- `return-inspection-ui.mjs`: 18 kiểm tra giao diện qua với Edge headless và API giả; có ảnh tại `artifacts/return-inspection/ui/`.
- `fix69-ui.mjs`: 27 kiểm tra hồi quy qua với API giả.
- Đã kiểm tra nhận từng phần, hàng đạt/không đạt, gửi trùng đồng thời, dữ liệu cũ trên hai màn hình, mã kiểm nhận đổi nội dung, vai trò không được ghi, nhận quá số lượng, nhật ký và giữ nguyên dữ liệu tiền/hàng đổi mới.
- Đã áp dụng script lên Supabase và kiểm tra schema/health chỉ đọc; tạo phiếu, duyệt và kiểm hàng thử vẫn chỉ thực hiện trên fixture PostgreSQL local. Chưa commit/push.
