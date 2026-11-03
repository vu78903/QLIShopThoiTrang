# Hạng thẻ, voucher và đề xuất sử dụng các bảng hiện có

## Quy tắc đã áp dụng

| Tổng chi tiêu đã ghi nhận trước hóa đơn hiện tại | Hạng | Ưu đãi |
|---|---|---:|
| Từ 0 đến đúng 5.000.000 đồng | Bạc | 0% |
| Trên 5.000.000 đến đúng 15.000.000 đồng | Vàng | 5% |
| Trên 15.000.000 đồng | Kim Cương | 10% |

Nhập số điện thoại khách trước rồi chọn hàng từ kho. POS tự nhận hạng theo tổng chi tiêu và tính ưu đãi trên toàn bộ tiền hàng, không yêu cầu nhập mã thành viên. API đối chiếu lại tổng chi tiêu trong database, không tin mức giảm do client tự gửi.

Giá sale của SKU được dùng làm đơn giá ban đầu. Nếu có voucher, so sánh **số tiền được giảm** của voucher với hạng thẻ và chọn mức cao hơn, không cộng dồn. Nếu bằng nhau thì ưu tiên thành viên. Voucher không được chọn không được ghi là voucher đã áp dụng trong hóa đơn.

Thứ tự: tiền hàng → ưu đãi tốt hơn → tiền hàng sau giảm → VAT 8% theo cấu hình hiện tại → cộng phí giao hàng nếu có. Phí giao hàng theo dõi riêng, không đưa vào tiền hàng để tính chiết khấu. Hạng dùng cho hóa đơn hiện tại được xác định trước khi cộng chi tiêu của chính hóa đơn đó. Đổi trả vẫn dựa trên số thực trả đã lưu của hóa đơn gốc.

Ví dụ tiền hàng 1.000.000 đồng, không voucher/phí giao:

| Hạng | Giảm | Tiền hàng sau giảm | VAT 8% | Phải trả |
|---|---:|---:|---:|---:|
| Bạc | 0 | 1.000.000 | 80.000 | 1.080.000 |
| Vàng | 50.000 | 950.000 | 76.000 | 1.026.000 |
| Kim Cương | 100.000 | 900.000 | 72.000 | 972.000 |

Vàng có voucher 10% thì chỉ giảm 100.000, tổng 972.000. Kim Cương có voucher 10.000 đồng thì giữ ưu đãi thành viên 100.000, tổng 972.000.

POS hiển thị riêng ưu đãi hạng thẻ, voucher và tiền hàng sau giảm. Khoản không được chọn ghi rõ “không áp dụng”. Hóa đơn lưu đúng tiền thực trả, dùng cho in lại và đổi trả.

## Ba bảng chưa được dùng để lưu nghiệp vụ

Đã kiểm tra Supabase ngày 03/11/2026: cả ba bảng đều có **0 dòng**. Tên bảng hàng lỗi thực tế là `bienbanxulyhangloi`. Mã nguồn hiện chỉ tham chiếu hai bảng chi tiết báo cáo/hàng lỗi khi kiểm tra sản phẩm có chứng từ trước khi xóa. Chốt ca phía form hiện lưu `shiftReports` trong localStorage, chưa ghi ba bảng này.

Đây là **đề xuất**, chưa triển khai và chưa sửa cấu trúc hoặc dữ liệu ba bảng.

| Bảng | Đề xuất sử dụng |
|---|---|
| `baocaodoanhthu` | Lưu phiếu chốt ca/ngày: mã báo cáo, nhân viên lập, thời gian, tiền thu, tiền mặt kiểm đếm, chênh lệch, giá vốn và lợi nhuận. Báo cáo đã chốt được lưu Supabase để máy khác tra cứu, thay cho chỉ lưu localStorage. |
| `chitietbaocaodoanhthu` | Chi tiết từng SKU thuộc phiếu báo cáo: số lượng bán/hoàn, doanh thu sau chiết khấu, giá vốn và lợi nhuận. Bấm báo cáo tổng để xem SKU đóng góp doanh thu và đối chiếu hóa đơn. |
| `bienbanxulyhangloi` | Biên bản xử lý hàng nhận lại không đạt chất lượng hoặc hàng hỏng phát hiện trong kho: SKU, số lượng, tình trạng, nguyên nhân, biện pháp sửa/đổi nhà cung cấp/hủy, người lập và người duyệt. Liên kết với chứng từ kiểm nhận gốc để không xử lý trùng. |

Khi triển khai báo cáo cần bổ sung kỳ báo cáo và trạng thái chốt, tránh chốt trùng cùng ca. Số liệu phải đối chiếu các hóa đơn đã thanh toán và phiếu hoàn tiền; không tính COD chưa thu tiền vào tiền đã thu. Tách VAT/phí giao khỏi doanh thu dùng tính lợi nhuận. Giá vốn phải có nguồn và thời điểm rõ ràng; không lấy giá bán hiện tại làm giá vốn.

Bảng hàng lỗi hiện chưa có số lượng/trạng thái duyệt/liên kết chứng từ, cần bổ sung trước khi sử dụng. Hàng khách trả đã bị loại khỏi tồn bán không được trừ kho bán thêm khi lập biên bản; hàng hỏng phát hiện từ kho bán chỉ trừ đúng một lần khi được duyệt. Chức năng thanh lý vẫn để sau theo yêu cầu.

## Kiểm chứng

Các script `tests/pos-returns/membership-api.mjs` và `membership-ui.mjs` kiểm tra ngưỡng hạng, hai SKU trong cùng hóa đơn, thứ tự chiết khấu/VAT, lựa chọn giữa voucher và thành viên, hai mức bằng nhau, loại bỏ ưu đãi khi bỏ khách và API từ chối cộng dồn trái quy tắc.

Kết quả và ảnh được lưu trong `artifacts/pos-returns/` (không đưa lên Git): `membership-api-results.json`, `membership-ui-results.json`, `membership-gold-voucher.png`, `membership-diamond.png`. Các kiểm thử API dùng dữ liệu TESTPOS riêng và được dọn sau khi chạy.
