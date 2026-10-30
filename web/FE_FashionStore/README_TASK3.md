# FashionStore — Customer Task 3

Bản tiếp tục từ source CheckoutOrders đính kèm. Mở `docs/CUSTOMER_RUN_GUIDE.md` trước khi chạy.

**Chưa build/runtime trên Windows; không phải bản nghiệm thu hoàn chỉnh.** Đã thêm các màn hình và API thực cho tài khoản, lọc sản phẩm, voucher, timeline/hủy đơn, điểm, wishlist, địa chỉ, review và yêu cầu đổi/trả. Những giới hạn nghiệp vụ và kiểm thử được ghi trong CUSTOMER_TEST_REPORT.md.

- Audit: docs/CUSTOMER_SOURCE_AUDIT.md
- Kế hoạch: docs/CUSTOMER_UI_IMPLEMENTATION_PLAN.md
- Thay đổi: docs/CUSTOMER_UI_CHANGELOG.md
- API: docs/CUSTOMER_API_MAPPING.md
- Test: docs/CUSTOMER_TEST_REPORT.md + CUSTOMER_STATIC_CHECKS.json
- Chạy/cấu hình/rollback/ảnh minh chứng: docs/CUSTOMER_RUN_GUIDE.md

Giữ source Task 2 riêng. Chạy migration 06 bằng SSMS trong FashionStoreDB_Dev sau khi xem script và backup. Không chạy lên production; không chạy lại seed. Source sạch không kèm packages/bin/obj/.vs nên cần Restore NuGet.
